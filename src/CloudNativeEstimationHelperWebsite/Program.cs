using CNEH_Web.Models;
using CNEH_Web.Services;
using CNEH_Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AzurePricesService>();
builder.Services.AddScoped<SettingsService>();

// Register a service to initialize AzurePricesService with configuration
builder.Services.AddScoped<IServiceInitializer, InitializeServices>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    // Initialize the AzurePricesService with the configured currency
    var initService = scope.ServiceProvider.GetRequiredService<IServiceInitializer>();
    await initService.InitializeAsync();
}
app.Run();
