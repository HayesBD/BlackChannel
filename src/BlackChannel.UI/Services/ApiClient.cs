using System.Net;
using System.Net.Http.Json;
using BlackChannel.Shared;

namespace BlackChannel.Web.Services;

/// <summary>Talks to the Functions backend. Attaches identity (dev header or bearer) per call.</summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly IUserSession _session;

    public ApiClient(HttpClient http, IUserSession session)
    {
        _http = http;
        _session = session;
    }

    // ---- keys ----

    public async Task PublishKeyAsync(KeyBundle bundle)
    {
        using var req = await BuildAsync(HttpMethod.Post, "keys", bundle);
        (await _http.SendAsync(req)).EnsureSuccessStatusCode();
    }

    public async Task<KeyBundle?> GetKeyAsync(string userId)
    {
        using var req = await BuildAsync(HttpMethod.Get, $"keys/{userId}");
        var res = await _http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var bundles = await res.Content.ReadFromJsonAsync<List<KeyBundle>>();
        return bundles?.FirstOrDefault(); // baseline: one device per user
    }

    // ---- messages ----

    /// <summary>Returns the new envelope id.</summary>
    public async Task<string> SendAsync(SealedEnvelope envelope)
    {
        using var req = await BuildAsync(HttpMethod.Post, "messages", envelope);
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<SendResult>();
        return body?.Id ?? "";
    }

    public async Task<List<SealedEnvelope>> SyncAsync()
    {
        using var req = await BuildAsync(HttpMethod.Get, "messages");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<SealedEnvelope>>() ?? new();
    }

    // ---- invites ----

    public async Task<CreateInviteResponse?> CreateInviteAsync()
    {
        using var req = await BuildAsync(HttpMethod.Post, "invites");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CreateInviteResponse>();
    }

    public async Task<RedeemInviteResponse?> RedeemInviteAsync(string code)
    {
        using var req = await BuildAsync(HttpMethod.Post, $"invites/{code}/redeem");
        var res = await _http.SendAsync(req);
        return await res.Content.ReadFromJsonAsync<RedeemInviteResponse>();
    }

    // ---- helpers ----

    private async Task<HttpRequestMessage> BuildAsync(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        if (body is not null) req.Content = JsonContent.Create(body);

        var token = await _session.GetAccessTokenAsync();
        if (token is not null)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        else
            req.Headers.Add("X-Dev-User", await _session.GetUserIdAsync()); // dev mode only

        return req;
    }

    private sealed class SendResult { public string Id { get; set; } = ""; }
}
