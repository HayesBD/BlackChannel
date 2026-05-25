using System.Net;
using BlackChannel.Functions.Auth;
using BlackChannel.Functions.Storage;
using BlackChannel.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace BlackChannel.Functions.Functions;

/// <summary>
/// Publishing and fetching <b>public</b> key bundles. This is the only key material the
/// server ever touches. Private keys live and die in the browser.
/// </summary>
public sealed class KeysFunctions
{
    private readonly UserResolver _users;
    private readonly KeyStore _keys;

    public KeysFunctions(UserResolver users, KeyStore keys)
    {
        _users = users;
        _keys = keys;
    }

    /// <summary>POST /api/keys — publish the caller's public key bundle for this device.</summary>
    [Function("PublishKeys")]
    public async Task<HttpResponseData> Publish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "keys")] HttpRequestData req)
    {
        var userId = await _users.ResolveAsync(req);
        if (userId is null) return await req.Unauthorized();

        var bundle = await req.ReadFromJsonAsync<KeyBundle>();
        if (bundle is null || string.IsNullOrWhiteSpace(bundle.IdentityPublicKey))
            return await req.Error(HttpStatusCode.BadRequest, "Missing identity public key.");

        // Always trust the authenticated identity over whatever the body claims.
        bundle.UserId = userId;
        await _keys.UpsertAsync(bundle);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    /// <summary>GET /api/keys/{userId} — fetch a user's published public key bundle(s).</summary>
    [Function("GetKeys")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "keys/{userId}")] HttpRequestData req,
        string userId)
    {
        var caller = await _users.ResolveAsync(req);
        if (caller is null) return await req.Unauthorized();

        var bundles = await _keys.GetBundlesAsync(userId);
        if (bundles.Count == 0)
            return await req.Error(HttpStatusCode.NotFound, "No key published for that user.");

        return await req.Json(bundles);
    }
}
