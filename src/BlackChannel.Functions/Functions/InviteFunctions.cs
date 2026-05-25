using System.Net;
using BlackChannel.Functions.Auth;
using BlackChannel.Functions.Storage;
using BlackChannel.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace BlackChannel.Functions.Functions;

/// <summary>
/// Shareable, single-use invite links. The invite is a routing handshake only — it
/// carries no key material. Keys are always fetched fresh from /api/keys.
/// </summary>
public sealed class InviteFunctions
{
    private static readonly TimeSpan InviteTtl = TimeSpan.FromDays(7);

    private readonly UserResolver _users;
    private readonly InviteStore _invites;

    public InviteFunctions(UserResolver users, InviteStore invites)
    {
        _users = users;
        _invites = invites;
    }

    /// <summary>POST /api/invites — mint a one-time invite link for the caller to share.</summary>
    [Function("CreateInvite")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invites")] HttpRequestData req)
    {
        var userId = await _users.ResolveAsync(req);
        if (userId is null) return await req.Unauthorized();

        var invite = await _invites.CreateAsync(userId, InviteTtl);
        var siteUrl = (Environment.GetEnvironmentVariable("PUBLIC_SITE_URL") ?? "").TrimEnd('/');

        return await req.Json(new CreateInviteResponse
        {
            Code = invite.RowKey,
            JoinUrl = $"{siteUrl}/join/{invite.RowKey}",
            ExpiresAt = invite.ExpiresAt
        });
    }

    /// <summary>POST /api/invites/{code}/redeem — redeem an invite and link the two users.</summary>
    [Function("RedeemInvite")]
    public async Task<HttpResponseData> Redeem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invites/{code}/redeem")] HttpRequestData req,
        string code)
    {
        var userId = await _users.ResolveAsync(req);
        if (userId is null) return await req.Unauthorized();

        var inviterId = await _invites.RedeemAsync(code, userId);
        if (inviterId is null)
        {
            return await req.Json(
                new RedeemInviteResponse { Success = false, Error = "Invite is invalid, expired, or already used." },
                HttpStatusCode.BadRequest);
        }

        return await req.Json(new RedeemInviteResponse { Success = true, InviterUserId = inviterId });
    }
}
