using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CloudNativeEstimationHelper;
using CloudNativeEstimationHelper.Services;
using CloudNativeEstimationHelper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

builder.Services.AddScoped<AzurePricesService>();
builder.Services.AddScoped<CloudNativeEstimationHelper.Services.SettingsService>();
builder.Services.AddLogging();

// Register a service to initialize AzurePricesService with configuration
builder.Services.AddScoped<IInitializeAzurePricesService, InitializeAzurePricesService>();

var host = builder.Build();

// Initialize the AzurePricesService with the configured currency
var initService = host.Services.GetRequiredService<IInitializeAzurePricesService>();
await initService.InitializeAsync();

await host.RunAsync();
