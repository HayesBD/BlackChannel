using BlackChannel.Web;
using BlackChannel.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var cfg = builder.Configuration;
var apiBase = cfg["apiBaseUrl"] ?? "/api";
if (!apiBase.EndsWith('/')) apiBase += "/"; // HttpClient drops the last segment otherwise.

// HttpClient.BaseAddress MUST be absolute. In production apiBaseUrl is an absolute
// function URL; locally it's "/api", so resolve it against the site's own origin
// (e.g. http://localhost:5173/) to get an absolute URI.
var apiBaseUri = Uri.TryCreate(apiBase, UriKind.Absolute, out var absolute)
    ? absolute
    : new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBase.TrimStart('/'));

var appConfig = new AppConfig
{
    ApiBaseUrl = apiBase,
    EntraAuthority = cfg["entra:authority"] ?? "",
    EntraClientId = cfg["entra:clientId"] ?? "",
    EntraScopes = cfg["entra:scopes"] ?? ""
};
builder.Services.AddSingleton(appConfig);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseUri });

// Identity. Dev-user mode for now (generates + persists a local user id). When Entra is
// configured, this is where MSAL (Microsoft.Authentication.WebAssembly.Msal) plugs in.
// Either way the rest of the app only depends on IUserSession.
builder.Services.AddScoped<IUserSession, UserSession>();

// Crypto. ECIES over WebCrypto today; swap the IMessageCrypto implementation for a
// Double Ratchet one later without touching the UI.
builder.Services.AddScoped<IMessageCrypto, CryptoService>();

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<RealtimeService>();

await builder.Build().RunAsync();
