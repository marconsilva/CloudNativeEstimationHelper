using System.Text.Json;
using Microsoft.JSInterop;

namespace CNEH_Web.Services
{
    /// <summary>
    /// Service to manage user settings and preferences
    /// </summary>
    public class SettingsService
    {
        string settingsCurrency = "USD"; // Default currency
        List<string> settingsRegions = new List<string> { "westeurope", "northeurope", "swedencentral" }; // Default regions

        public SettingsService()
        {
        }

        public Task InitializeAsync()
        {
            // Initialization logic if needed
            return Task.CompletedTask;
        }

        /// <summary>
        /// Save currency preference to cookie
        /// </summary>
        public async Task SaveCurrencyPreferenceAsync(string currency)
        {
            settingsCurrency = currency;
            await Task.CompletedTask;

        }

        /// <summary>
        /// Get saved currency preference from cookie
        /// </summary>
        public async Task<string> GetCurrencyPreferenceAsync()
        {
            return await Task.FromResult(settingsCurrency);
        }

        /// <summary>
        /// Save regions preference to cookie
        /// </summary>
        public async Task SaveRegionsPreferenceAsync(IEnumerable<string> regions)
        {
            settingsRegions = regions.ToList();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get saved regions preference from cookie
        /// </summary>
        public async Task<IEnumerable<string>> GetRegionsPreferenceAsync()
        {
            return await Task.FromResult(settingsRegions);
        }
    }
}
