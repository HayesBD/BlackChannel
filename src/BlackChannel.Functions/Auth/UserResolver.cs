using System.IdentityModel.Tokens.Jwt;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using HttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace BlackChannel.Functions.Auth;

/// <summary>
/// Resolves the calling user id from an incoming request.
///
/// Two modes, chosen at runtime:
///   • Production: when ENTRA_AUTHORITY + ENTRA_AUDIENCE are set, validate the
///     <c>Authorization: Bearer &lt;jwt&gt;</c> token against Entra External ID and use
///     its subject claim.
///   • Local dev: when those are not set, trust an <c>X-Dev-User</c> header (or fall
///     back to "dev-user"). This path must NEVER run in production — it has no security.
///
/// The user id is routing identity only. It never unlocks message content; the server
/// holds no keys.
/// </summary>
public sealed class UserResolver
{
    private readonly string? _authority;
    private readonly string? _audience;
    private readonly bool _devMode;
    private ConfigurationManager<OpenIdConnectConfiguration>? _configManager;

    public UserResolver()
    {
        _authority = Environment.GetEnvironmentVariable("ENTRA_AUTHORITY");
        _audience  = Environment.GetEnvironmentVariable("ENTRA_AUDIENCE");
        _devMode   = string.IsNullOrWhiteSpace(_authority) || string.IsNullOrWhiteSpace(_audience);
    }

    public bool IsDevMode => _devMode;

    /// <summary>Returns the authenticated user id, or null if the request is unauthenticated.</summary>
    public async Task<string?> ResolveAsync(HttpRequestData req)
    {
        if (_devMode)
        {
            if (req.Headers.TryGetValues("X-Dev-User", out var devValues))
            {
                var dev = devValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(dev)) return dev;
            }
            return "dev-user";
        }

        if (!req.Headers.TryGetValues("Authorization", out var authValues))
            return null;

        var header = authValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header["Bearer ".Length..].Trim();

        _configManager ??= new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_authority!.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());

        var oidc = await _configManager.GetConfigurationAsync(CancellationToken.None);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = oidc.Issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            IssuerSigningKeys = oidc.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out _);

            // Entra External ID puts the stable user id in 'sub' (or 'oid').
            return principal.FindFirst("sub")?.Value
                   ?? principal.FindFirst("oid")?.Value;
        }
        catch
        {
            return null; // invalid/expired token
        }
    }
}
