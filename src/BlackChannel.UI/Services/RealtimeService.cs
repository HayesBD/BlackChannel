using System.Net.Http.Json;
using BlackChannel.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlackChannel.Web.Services;

/// <summary>
/// Live delivery over Azure SignalR. Connects using the token from /api/negotiate and
/// raises <see cref="EnvelopeReceived"/> when a ciphertext envelope is pushed. Carries
/// ciphertext only — decryption happens in the page via IMessageCrypto.
/// </summary>
public sealed class RealtimeService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly IUserSession _session;
    private HubConnection? _hub;

    public RealtimeService(HttpClient http, IUserSession session)
    {
        _http = http;
        _session = session;
    }

    public event Action<SealedEnvelope>? EnvelopeReceived;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    /// <summary>
    /// Connects to SignalR for live delivery. Best-effort: if negotiate or the connection
    /// fails (e.g. SignalR not configured locally), it returns false rather than throwing —
    /// the app still works via GET /api/messages (sync). Returns true if connected.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (_hub is not null) return IsConnected;

        try
        {
            var userId = await _session.GetUserIdAsync();

            // Ask the backend for a SignalR connection token scoped to this user.
            using var negReq = new HttpRequestMessage(HttpMethod.Post, "negotiate");
            negReq.Headers.Add("x-bc-user", userId);
            var negRes = await _http.SendAsync(negReq);
            negRes.EnsureSuccessStatusCode();
            var info = await negRes.Content.ReadFromJsonAsync<NegotiateInfo>()
                       ?? throw new InvalidOperationException("Negotiate returned no connection info.");

            _hub = new HubConnectionBuilder()
                .WithUrl(info.Url, opt => opt.AccessTokenProvider = () => Task.FromResult(info.AccessToken)!)
                .WithAutomaticReconnect()
                .Build();

            _hub.On<SealedEnvelope>("newMessage", env => EnvelopeReceived?.Invoke(env));

            await _hub.StartAsync();
            return true;
        }
        catch
        {
            // Live push unavailable — not fatal. Messages still arrive on the next sync.
            _hub = null;
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }

    private sealed class NegotiateInfo
    {
        public string Url { get; set; } = "";
        public string AccessToken { get; set; } = "";
    }
}
