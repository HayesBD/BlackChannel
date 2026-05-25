namespace BlackChannel.Web;

/// <summary>Runtime config baked into wwwroot/appsettings.json at deploy time.</summary>
public sealed class AppConfig
{
    public string ApiBaseUrl { get; set; } = "/api";

    // Entra External ID. When EntraClientId is blank, the app runs in dev-user mode
    // (no real sign-in). Set these to require Entra External ID sign-in.
    public string EntraAuthority { get; set; } = "";
    public string EntraClientId { get; set; } = "";
    public string EntraScopes { get; set; } = "";

    public bool AuthConfigured => !string.IsNullOrWhiteSpace(EntraClientId);
}
