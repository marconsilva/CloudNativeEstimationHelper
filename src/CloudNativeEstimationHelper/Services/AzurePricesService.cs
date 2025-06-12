using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using CloudNativeEstimationHelper.Models;

namespace CloudNativeEstimationHelper.Services
{
    /// <summary>
    /// Service responsible for loading and retrieving Azure pricing information
    /// </summary>
    public class AzurePricesService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzurePricesService> _logger;
        private Dictionary<string, List<AzurePriceItem>> _pricesByService = new();
        private string _currencyCode = "USD"; // Default currency
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        // List of allowed regions for filtering
        private readonly List<string> _configuredRegions = new()
        {
            "westeurope",
            "northeurope",
            "swedencentral"
        };

        // Flag to enable/disable region filtering
        private bool _filterByRegion = true;

        // Azure Pricing API URL
        private const string BaseAddressApiUrl = "https://prices.azure.com/";
        private const string BaseApiUrl = "/api/retail/prices";
        private const string ApiVersion = "2023-01-01-preview";

        public AzurePricesService(ILogger<AzurePricesService> logger)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseAddressApiUrl) };
            _logger = logger;
        }

        /// <summary>
        /// Initialize the service by loading all Azure prices
        /// </summary>
        /// <param name="currencyCode">The currency code for pricing (e.g., USD, EUR)</param>
        public async Task InitializeAsync(string currencyCode = "USD")
        {
            // Prevent concurrent initializations
            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized && _currencyCode == currencyCode)
                {
                    return;
                }

                _currencyCode = currencyCode;
                _pricesByService.Clear();

                _logger.LogInformation("Initializing Azure Prices Service with currency: {Currency}", currencyCode);
                await LoadAllPricesAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Prices Service");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }    /// <summary>
             /// Load all prices from Azure API with pagination support
             /// </summary>
        private async Task LoadAllPricesAsync()
        {
            string? nextPageLink = null;
            int pageCount = 0;
            const int maxPages = 50; // Safety limit to prevent infinite loops

            try
            {
                // Build initial URL with filter if region filtering is enabled
                string initialUrl = $"{BaseApiUrl}?api-version={ApiVersion}&currencyCode={_currencyCode}";

                // Add region filter to the URL if enabled
                if (_filterByRegion && _configuredRegions.Count > 0)
                {
                    // Build a filter that includes all configured regions: location eq 'region1' or location eq 'region2' or ...
                    var regionFilters = _configuredRegions
                        .Select(region => $"armRegionName eq '{FormatRegionName(region)}'")
                        .ToList();

                    string filterQuery = string.Join(" or ", regionFilters);
                    initialUrl += $"&$filter={Uri.EscapeDataString(filterQuery)}";

                    _logger.LogInformation("Using region filter: {Filter}", filterQuery);
                }

                string responseString = await _httpClient.GetStringAsync(initialUrl);

                var response = JsonSerializer.Deserialize<AzurePriceResponse>(responseString);

                if (response == null)
                {
                    _logger.LogError("Failed to retrieve Azure prices: empty response");
                    return;
                }

                ProcessPriceItems(response.Items);
                nextPageLink = response.NextPageLink;
                pageCount++;

                // Get remaining pages
                while (!string.IsNullOrEmpty(nextPageLink) && pageCount < maxPages)
                {
                    _logger.LogInformation("Loading page {PageCount} of Azure prices", pageCount + 1);
                    response = await _httpClient.GetFromJsonAsync<AzurePriceResponse>(nextPageLink);

                    if (response == null)
                    {
                        break;
                    }

                    ProcessPriceItems(response.Items);
                    nextPageLink = response.NextPageLink;
                    pageCount++;
                }

                _logger.LogInformation("Loaded {PageCount} pages of Azure prices with {ItemCount} items for {ServiceCount} services",
                    pageCount, _pricesByService.Values.Sum(list => list.Count), _pricesByService.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Azure prices");
                throw;
            }
        }    /// <summary>
             /// Processes price items and organizes them by service name
             /// </summary>
        private void ProcessPriceItems(List<AzurePriceItem>? items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            foreach (var item in items)
            {
                // Skip items with missing service name
                if (string.IsNullOrEmpty(item.ServiceName))
                {
                    continue;
                }

                // Since we filter by location in the API call,
                // we don't need to filter here when _filterByRegion is true

                // Add safety check for items that might pass through the API filter
                // but still don't match our regions (just in case)
                if (_filterByRegion && !string.IsNullOrEmpty(item.Location))
                {
                    bool isMatchingRegion = _configuredRegions
                        .Select(r => FormatRegionName(r))
                        .Any(r => r.Equals(item.Location, StringComparison.OrdinalIgnoreCase));

                    if (!isMatchingRegion)
                    {
                        _logger.LogDebug("Skipping item with location {Location} not in configured regions", item.Location);
                        continue;
                    }
                }

                if (!_pricesByService.ContainsKey(item.ServiceName))
                {
                    _pricesByService[item.ServiceName] = new List<AzurePriceItem>();
                }

                _pricesByService[item.ServiceName].Add(item);
            }
        }

        /// <summary>
        /// Get prices for a specific Azure service
        /// </summary>
        /// <param name="serviceName">The name of the Azure service</param>
        /// <returns>List of price items for the specified service</returns>
        public async Task<IEnumerable<AzurePriceItem>> GetPricesForServiceAsync(string serviceName)
        {
            if (!_isInitialized)
            {
                await InitializeAsync(_currencyCode);
            }

            if (_pricesByService.TryGetValue(serviceName, out var prices))
            {
                return prices;
            }

            return Enumerable.Empty<AzurePriceItem>();
        }

        /// <summary>
        /// Get all available Azure service names
        /// </summary>
        /// <returns>List of all service names</returns>
        public async Task<IEnumerable<string>> GetServiceNamesAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync(_currencyCode);
            }

            return _pricesByService.Keys.OrderBy(k => k).ToList();
        }

        /// <summary>
        /// Get all prices for all services
        /// </summary>
        /// <returns>Dictionary of service names and their price items</returns>
        public async Task<IReadOnlyDictionary<string, List<AzurePriceItem>>> GetAllPricesAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync(_currencyCode);
            }

            return _pricesByService;
        }    /// <summary>
             /// Search for price items matching the specified criteria
             /// </summary>
             /// <param name="serviceName">Optional service name filter</param>
             /// <param name="meterName">Optional meter name filter</param>
             /// <param name="skuName">Optional SKU name filter</param>
             /// <param name="location">Optional location filter</param>
             /// <returns>List of matching price items</returns>
        public async Task<IEnumerable<AzurePriceItem>> SearchPricesAsync(
            string? serviceName = null,
            string? meterName = null,
            string? skuName = null,
            string? location = null)
        {
            if (!_isInitialized)
            {
                await InitializeAsync(_currencyCode);
            }

            IEnumerable<AzurePriceItem> results = _pricesByService
                .SelectMany(kv => kv.Value)
                .AsEnumerable();

            // Apply filters if specified
            if (!string.IsNullOrEmpty(serviceName))
            {
                results = results.Where(p => p.ServiceName?.Contains(serviceName, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrEmpty(meterName))
            {
                results = results.Where(p => p.MeterName?.Contains(meterName, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrEmpty(skuName))
            {
                results = results.Where(p => p.SkuName?.Contains(skuName, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrEmpty(location))
            {
                results = results.Where(p => p.Location?.Contains(location, StringComparison.OrdinalIgnoreCase) == true);
            }

            return results.ToList();
        }

        /// <summary>
        /// Enable or disable region filtering
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        public void SetRegionFiltering(bool enable)
        {
            // If changing the filter setting, we need to reinitialize
            if (_filterByRegion != enable && _isInitialized)
            {
                _isInitialized = false;
            }

            _filterByRegion = enable;
        }

        /// <summary>
        /// Set the list of regions to filter by
        /// </summary>
        /// <param name="regions">List of region names</param>
        public void SetConfiguredRegions(IEnumerable<string> regions)
        {
            // Normalize the region names
            var normalizedRegions = regions.Select(r => r.Replace(" ", "").ToLowerInvariant()).ToList();

            // Check if the list has changed
            bool regionsChanged = !_configuredRegions.SequenceEqual(normalizedRegions);

            // Update the list if changed
            if (regionsChanged)
            {
                _configuredRegions.Clear();
                _configuredRegions.AddRange(normalizedRegions);

                // If we've already initialized, we need to reinitialize with the new regions
                if (_isInitialized)
                {
                    _isInitialized = false;
                }
            }
        }

        /// <summary>
        /// Get the currently configured regions
        /// </summary>
        /// <returns>List of configured region names</returns>
        public IReadOnlyList<string> GetConfiguredRegions()
        {
            return _configuredRegions.AsReadOnly();
        }

        /// <summary>
        /// Check if region filtering is enabled
        /// </summary>
        /// <returns>True if filtering is enabled, false otherwise</returns>
        public bool IsRegionFilteringEnabled()
        {
            return _filterByRegion;
        }

        /// <summary>
        /// Format a region name for use in the API filter
        /// </summary>
        /// <param name="region">Region name (e.g., "westeurope")</param>
        /// <returns>Properly formatted region name (e.g., "West Europe")</returns>
        private string FormatRegionName(string region)
        {
            // The API expects region names like "West Europe" instead of "westeurope"
            switch (region.ToLowerInvariant().Replace(" ", ""))
            {
                case "westeurope":
                    return "westeurope";
                case "northeurope":
                    return "northeurope";
                case "swedencentral":
                    return "swedencentral";
                default:
                    return "northeurope";
            }
        }
    }
}
