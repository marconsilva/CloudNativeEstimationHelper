using System.Text.Json;
using Microsoft.JSInterop;

namespace CloudNativeEstimationHelper.Services
{
    /// <summary>
    /// Service to manage user settings and preferences
    /// </summary>
    public class SettingsService
    {
        private readonly IJSRuntime _jsRuntime;
        private const string CurrencyCookieName = "azureprices_currency";
        private const string RegionsCookieName = "azureprices_regions";
        private const int CookieExpirationDays = 365; // 1 year

        public SettingsService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Save currency preference to cookie
        /// </summary>
        public async Task SaveCurrencyPreferenceAsync(string currency)
        {
            await SetCookieAsync(CurrencyCookieName, currency);
        }

        /// <summary>
        /// Get saved currency preference from cookie
        /// </summary>
        public async Task<string> GetCurrencyPreferenceAsync(string defaultCurrency = "USD")
        {
            var cookie = await GetCookieAsync(CurrencyCookieName);
            return string.IsNullOrEmpty(cookie) ? defaultCurrency : cookie;
        }

        /// <summary>
        /// Save regions preference to cookie
        /// </summary>
        public async Task SaveRegionsPreferenceAsync(IEnumerable<string> regions)
        {
            var regionsJson = JsonSerializer.Serialize(regions);
            await SetCookieAsync(RegionsCookieName, regionsJson);
        }

        /// <summary>
        /// Get saved regions preference from cookie
        /// </summary>
        public async Task<IEnumerable<string>> GetRegionsPreferenceAsync()
        {
            var cookie = await GetCookieAsync(RegionsCookieName);
            
            if (string.IsNullOrEmpty(cookie))
            {
                // Default regions
                return new List<string> { "westeurope", "northeurope", "swedencentral" };
            }

            try
            {
                return JsonSerializer.Deserialize<IEnumerable<string>>(cookie) ?? 
                    new List<string> { "westeurope", "northeurope", "swedencentral" };
            }
            catch
            {
                // If deserialization fails, return defaults
                return new List<string> { "westeurope", "northeurope", "swedencentral" };
            }
        }

        /// <summary>
        /// Set a cookie with the given name and value
        /// </summary>
        private async Task SetCookieAsync(string name, string value)
        {
            // Calculate expiration date
            var expiryDate = DateTime.Now.AddDays(CookieExpirationDays).ToString("r");
            
            // Set cookie with path and expiration
            await _jsRuntime.InvokeVoidAsync("eval", 
                $"document.cookie = \"{name}={value}; path=/; expires={expiryDate}\"");
        }

        /// <summary>
        /// Get the value of a cookie by name
        /// </summary>
        private async Task<string> GetCookieAsync(string name)
        {
            // JavaScript function to get cookie by name
            var cookieScript = @"
                function getCookie(name) {
                    var nameEQ = name + ""="";
                    var ca = document.cookie.split(';');
                    for(var i=0;i < ca.length;i++) {
                        var c = ca[i];
                        while (c.charAt(0)==' ') c = c.substring(1,c.length);
                        if (c.indexOf(nameEQ) == 0) return c.substring(nameEQ.length,c.length);
                    }
                    return '';
                }
                getCookie('" + name + "')";

            return await _jsRuntime.InvokeAsync<string>("eval", cookieScript);
        }
    }
}
