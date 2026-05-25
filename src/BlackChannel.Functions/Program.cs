using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using BlackChannel.Functions.Auth;
using BlackChannel.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // ---------------------------------------------------------------------
        // Storage clients. Two modes, picked by environment:
        //   • STORAGE_ACCOUNT_NAME set  -> Azure, via managed identity (no secrets).
        //   • otherwise                 -> local Azurite, via AzureWebJobsStorage
        //                                  connection string ("UseDevelopmentStorage=true").
        // The application NEVER uses storage account keys directly in Azure.
        // ---------------------------------------------------------------------
        var account = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME");

        if (!string.IsNullOrWhiteSpace(account))
        {
            var cred = new DefaultAzureCredential();
            services.AddSingleton(new TableServiceClient(
                new Uri($"https://{account}.table.core.windows.net"), cred));
            services.AddSingleton(new BlobServiceClient(
                new Uri($"https://{account}.blob.core.windows.net"), cred));
        }
        else
        {
            var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                       ?? "UseDevelopmentStorage=true";
            services.AddSingleton(new TableServiceClient(conn));
            services.AddSingleton(new BlobServiceClient(conn));
        }

        services.AddSingleton<KeyStore>();
        services.AddSingleton<InviteStore>();
        services.AddSingleton<EnvelopeStore>();

        // Resolves the caller's identity from a validated Entra JWT, or (local only)
        // from an X-Dev-User header. See UserResolver.
        services.AddSingleton<UserResolver>();
    })
    .Build();

await host.RunAsync();
