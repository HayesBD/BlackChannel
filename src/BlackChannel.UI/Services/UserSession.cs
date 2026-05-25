using Microsoft.JSInterop;

namespace BlackChannel.Web.Services;

/// <summary>
/// Who the current user is, from the rest of the app's point of view. The UI and API
/// client depend only on this — so swapping dev-user mode for real Entra sign-in is a
/// one-class change.
/// </summary>
public interface IUserSession
{
    /// <summary>Stable user id used for routing (NOT a key — keys are per-device crypto).</summary>
    Task<string> GetUserIdAsync();

    /// <summary>Bearer token to attach to API calls, or null in dev-user mode.</summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>True when running without a real identity provider (local/dev only).</summary>
    bool IsDevMode { get; }
}

/// <summary>
/// Dev-user implementation: generates and persists a random user id in localStorage so a
/// browser keeps a stable identity across reloads without any sign-in. Production swaps
/// this for an MSAL-backed implementation wired to Entra External ID.
/// </summary>
public sealed class UserSession : IUserSession
{
    private readonly IJSRuntime _js;
    private readonly AppConfig _config;
    private string? _cached;

    public UserSession(IJSRuntime js, AppConfig config)
    {
        _js = js;
        _config = config;
    }

    public bool IsDevMode => !_config.AuthConfigured;

    public async Task<string> GetUserIdAsync()
    {
        if (_cached is not null) return _cached;

        // When Entra is configured this is where the signed-in user's id (token 'sub')
        // would come from. For now, a persisted local id.
        var existing = await _js.InvokeAsync<string?>("localStorage.getItem", "bc-user-id");
        if (string.IsNullOrWhiteSpace(existing))
        {
            existing = "user-" + Guid.NewGuid().ToString("N")[..12];
            await _js.InvokeVoidAsync("localStorage.setItem", "bc-user-id", existing);
        }
        return _cached = existing;
    }

    public Task<string?> GetAccessTokenAsync()
        => Task.FromResult<string?>(null); // dev mode: no bearer token; API uses X-Dev-User
}
