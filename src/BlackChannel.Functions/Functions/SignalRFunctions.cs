using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace BlackChannel.Functions.Functions;

/// <summary>
/// SignalR negotiate endpoint. Hands the client a connection token scoped to its user id
/// so the SignalR service routes that user's pushes to this connection.
/// </summary>
public sealed class SignalRFunctions
{
    /// <summary>
    /// POST /api/negotiate — returns SignalR connection info for the user named in the
    /// <c>x-bc-user</c> header.
    ///
    /// NOTE: the binding expression below reads the user id from a header the client sets.
    /// That's fine for the dev/scaffold path. In production, route this through the SWA /
    /// EasyAuth principal header (e.g. <c>{headers.x-ms-client-principal-name}</c>) or a
    /// validated token so a client can't impersonate another user's mailbox.
    /// </summary>
    [Function("Negotiate")]
    public static string Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequestData req,
        [SignalRConnectionInfoInput(HubName = "messages", UserId = "{headers.x-bc-user}")] string connectionInfo)
        => connectionInfo;
}
