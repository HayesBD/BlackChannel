using BlackChannel.Web;            // AppConfig
using BlackChannel.Web.Services;   // IUserSession, IMessageCrypto, ApiClient, RealtimeService
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;

namespace BlackChannel.App;

public static class MauiProgram
{
    // The BlazorWebView serves its pages from a loopback scheme, so there's no site origin
    // to resolve "/api" against (unlike the web build). Point the clients at the deployed
    // backend. SELF-HOSTERS: change this to YOUR Function App URL, then rebuild the app.
    private const string ApiBaseUrl = "https://func-blackchannel.azurewebsites.net/api/";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

        var apiBase = ApiBaseUrl.EndsWith('/') ? ApiBaseUrl : ApiBaseUrl + "/";
        builder.Services.AddSingleton(new AppConfig { ApiBaseUrl = apiBase });
        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });

        builder.Services.AddScoped<IUserSession, UserSession>();
        builder.Services.AddScoped<IMessageCrypto, CryptoService>();
        builder.Services.AddScoped<ApiClient>();
        builder.Services.AddScoped<RealtimeService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
