using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CloudNativeEstimationHelper.Services;

public interface IInitializeAzurePricesService
{
    Task InitializeAsync();
}

/// <summary>
/// Service to initialize the AzurePricesService with configuration values
/// </summary>
public class InitializeAzurePricesService : IInitializeAzurePricesService
{
    private readonly AzurePricesService _azurePricesService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InitializeAzurePricesService> _logger;

    public InitializeAzurePricesService(
        AzurePricesService azurePricesService,
        IConfiguration configuration,
        ILogger<InitializeAzurePricesService> logger)
    {
        _azurePricesService = azurePricesService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the AzurePricesService with the configured currency
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var currencyCode = _configuration["AzurePricing:DefaultCurrencyCode"] ?? "EUR";
            _logger.LogInformation("Initializing AzurePricesService with currency: {Currency}", currencyCode);
            await _azurePricesService.InitializeAsync(currencyCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AzurePricesService");
        }
    }
}
