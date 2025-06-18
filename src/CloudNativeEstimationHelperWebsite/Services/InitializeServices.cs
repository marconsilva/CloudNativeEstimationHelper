namespace CNEH_Web.Services
{
    
    public class InitializeServices : IServiceInitializer
    {
        private readonly AzurePricesService _azurePricesService;
        private readonly SettingsService _settingsService;

        public InitializeServices(AzurePricesService azurePricesService, SettingsService settingsService)
        {
            _azurePricesService = azurePricesService;
            _settingsService = settingsService;
        }

        public async Task InitializeAsync()
        {
            await _azurePricesService.InitializeAsync();
            await _settingsService.InitializeAsync();
        }
    }
}
