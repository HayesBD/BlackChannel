using System.Net;
using BlackChannel.Functions.Auth;
using BlackChannel.Functions.Storage;
using BlackChannel.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace BlackChannel.Functions.Functions;

/// <summary>
/// The blind mailbox. Accepts opaque ciphertext envelopes, stores them, and pushes a
/// realtime notification to the recipient via SignalR. The server cannot read any of it.
/// </summary>
public sealed class MessageFunctions
{
    private const string Hub = "messages";

    private readonly UserResolver _users;
    private readonly EnvelopeStore _envelopes;

    public MessageFunctions(UserResolver users, EnvelopeStore envelopes)
    {
        _users = users;
        _envelopes = envelopes;
    }

    /// <summary>POST /api/messages — send one sealed envelope.</summary>
    [Function("SendMessage")]
    public async Task<SendMessageOutput> Send(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messages")] HttpRequestData req)
    {
        var userId = await _users.ResolveAsync(req);
        if (userId is null)
            return new SendMessageOutput { HttpResponse = await req.Unauthorized() };

        var envelope = await req.ReadFromJsonAsync<SealedEnvelope>();
        if (envelope is null ||
            string.IsNullOrWhiteSpace(envelope.To) ||
            string.IsNullOrWhiteSpace(envelope.Ciphertext) ||
            string.IsNullOrWhiteSpace(envelope.EphemeralPublicKey) ||
            string.IsNullOrWhiteSpace(envelope.Iv))
        {
            return new SendMessageOutput
            {
                HttpResponse = await req.Error(HttpStatusCode.BadRequest, "Malformed envelope.")
            };
        }

        envelope.From = userId;                 // trust the authenticated sender, not the body
        envelope.Id = Guid.NewGuid().ToString("N");
        envelope.SentAt = DateTimeOffset.UtcNow;

        await _envelopes.StoreAsync(envelope);

        // Realtime push to the recipient. Carries ciphertext only — SignalR is a pipe.
        return new SendMessageOutput
        {
            Notification = new SignalRMessageAction("newMessage")
            {
                UserId = envelope.To,
                Arguments = new object[] { envelope }
            },
            HttpResponse = await req.Json(new { envelope.Id }, HttpStatusCode.Accepted)
        };
    }

    /// <summary>GET /api/messages — pull (and clear) the caller's pending envelopes.</summary>
    [Function("SyncMessages")]
    public async Task<HttpResponseData> Sync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "messages")] HttpRequestData req)
    {
        var userId = await _users.ResolveAsync(req);
        if (userId is null) return await req.Unauthorized();

        var pending = await _envelopes.ListPendingAsync(userId);

        // Delivered = deletable. The mailbox is transient by design.
        foreach (var e in pending)
            await _envelopes.DeleteAsync(userId, e.Id);

        return await req.Json(pending);
    }
}

/// <summary>Multi-output: an HTTP response plus an optional SignalR push.</summary>
public sealed class SendMessageOutput
{
    [SignalROutput(HubName = "messages")]
    public SignalRMessageAction? Notification { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
