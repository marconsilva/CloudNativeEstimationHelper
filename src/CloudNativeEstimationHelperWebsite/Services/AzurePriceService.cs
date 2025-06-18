using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using CNEH_Web.Models;

namespace CNEH_Web.Services
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

        private readonly string _cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cachedPricesList.json");
        private readonly TimeSpan _cacheValidity = TimeSpan.FromHours(24);


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
        private const string BaseAddressApiUrl = "https://prices.azure.com";
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
        }

        /// <summary>
        /// Load all prices from Azure API with pagination support
        /// </summary>
        private async Task LoadAllPricesAsync()
        {
            try
            {
                // Check if we can use cached data
                if (await TryLoadFromCacheAsync())
                {
                    _logger.LogInformation("Loaded Azure prices from cache file");
                    return;
                }
                string? nextPageLink = null;
                int pageCount = 0;
                const int maxPages = 50; // Safety limit to prevent infinite loops

                // Build initial URL with filter if region filtering is enabled
                string initialUrl = $"{BaseAddressApiUrl}{BaseApiUrl}?api-version={ApiVersion}&currencyCode={_currencyCode}";

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

                // Create request message
                var request = new HttpRequestMessage(HttpMethod.Get, initialUrl);

                // Send request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();

                var jsonResponse = JsonSerializer.Deserialize<AzurePriceResponse>(responseString);

                if (jsonResponse == null)
                {
                    _logger.LogError("Failed to retrieve Azure prices: empty response");
                    return;
                }

                ProcessPriceItems(jsonResponse.Items);
                nextPageLink = jsonResponse.NextPageLink;
                pageCount++;

                // Get remaining pages
                while (!string.IsNullOrEmpty(nextPageLink) && pageCount < maxPages)
                {
                    _logger.LogInformation("Loading page {PageCount} of Azure prices", pageCount + 1);
                    var nextPageRequest = new HttpRequestMessage(HttpMethod.Get, nextPageLink);

                    // Send request
                    var nextPageResponse = await _httpClient.SendAsync(nextPageRequest);
                    nextPageResponse.EnsureSuccessStatusCode();

                    if (nextPageResponse == null)
                    {
                        break;
                    }

                    var jsonNextPageResponse = JsonSerializer.Deserialize<AzurePriceResponse>(await nextPageResponse.Content.ReadAsStringAsync());
                    if (jsonNextPageResponse == null)
                    {
                        _logger.LogError("Failed to retrieve Azure prices: empty response on page {PageCount}", pageCount + 1);
                        break;
                    }

                    ProcessPriceItems(jsonNextPageResponse.Items);
                    nextPageLink = jsonNextPageResponse.NextPageLink;
                    pageCount++;
                }

                _logger.LogInformation("Loaded {PageCount} pages of Azure prices with {ItemCount} items for {ServiceCount} services",
                    pageCount, _pricesByService.Values.Sum(list => list.Count), _pricesByService.Count);

                // Save the newly loaded prices to cache
                await SaveToCacheAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Azure prices");
                throw;
            }
        }

        
        /// <summary>
        /// Try to load pricing data from cache file if it exists and is not expired
        /// </summary>
        /// <returns>True if cache was successfully loaded, false otherwise</returns>
        private async Task<bool> TryLoadFromCacheAsync()
        {
            try
            {
                // Check if cache file exists
                if (!File.Exists(_cachePath))
                {
                    _logger.LogInformation("Cache file does not exist");
                    return false;
                }

                // Check if cache file is valid (not older than 24 hours)
                var fileInfo = new FileInfo(_cachePath);
                if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > _cacheValidity)
                {
                    _logger.LogInformation("Cache file is expired (older than {Hours} hours)", _cacheValidity.TotalHours);
                    return false;
                }

                // Check if cache file is for current currency and region settings
                using (var stream = File.OpenRead(_cachePath))
                {
                    // Read metadata first
                    using var document = await JsonDocument.ParseAsync(stream);
                    var root = document.RootElement;
                    
                    if (!root.TryGetProperty("metadata", out var metadataElement))
                    {
                        _logger.LogWarning("Cache file is missing metadata");
                        return false;
                    }
                    
                    string? cachedCurrency = null;
                    bool? cachedRegionFiltering = null;
                    List<string>? cachedRegions = null;
                    
                    if (metadataElement.TryGetProperty("currencyCode", out var currencyElement))
                    {
                        cachedCurrency = currencyElement.GetString();
                    }
                    
                    if (metadataElement.TryGetProperty("filterByRegion", out var filterElement))
                    {
                        cachedRegionFiltering = filterElement.GetBoolean();
                    }
                    
                    if (metadataElement.TryGetProperty("configuredRegions", out var regionsElement))
                    {
                        cachedRegions = new List<string>();
                        foreach (var region in regionsElement.EnumerateArray())
                        {
                            cachedRegions.Add(region.GetString() ?? string.Empty);
                        }
                    }
                    
                    // Check if settings match
                    if (cachedCurrency != _currencyCode || 
                        cachedRegionFiltering != _filterByRegion ||
                        !SequenceEqual(cachedRegions, _configuredRegions))
                    {
                        _logger.LogInformation("Cache settings don't match current settings");
                        return false;
                    }
                }

                // Read the actual price data
                string json = await File.ReadAllTextAsync(_cachePath);
                var cacheData = JsonSerializer.Deserialize<CachePriceData>(json);
                
                if (cacheData == null || cacheData.PricesByService == null)
                {
                    _logger.LogWarning("Cache file contains invalid data");
                    return false;
                }
                
                _pricesByService = cacheData.PricesByService;
                _logger.LogInformation("Loaded cache with {ItemCount} items for {ServiceCount} services", 
                    _pricesByService.Values.Sum(list => list.Count), _pricesByService.Count);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading from cache file");
                return false;
            }
        }

        /// <summary>
        /// Save current pricing data to the cache file
        /// </summary>
        private async Task SaveToCacheAsync()
        {
            try
            {
                var cacheData = new CachePriceData
                {
                    Metadata = new CacheMetadata
                    {
                        CurrencyCode = _currencyCode,
                        FilterByRegion = _filterByRegion,
                        ConfiguredRegions = _configuredRegions,
                        CreatedAt = DateTime.UtcNow
                    },
                    PricesByService = _pricesByService
                };

                string json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(_cachePath, json);
                _logger.LogInformation("Saved Azure prices to cache file");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving to cache file");
                // We don't throw here as this is not critical for operation
            }
        }

        /// <summary>
        /// Helper method to compare two sequences for equality
        /// </summary>
        private bool SequenceEqual<T>(IEnumerable<T>? first, IEnumerable<T>? second)
        {
            if (first == null && second == null)
                return true;
            if (first == null || second == null)
                return false;
                
            return first.SequenceEqual(second);
        }

        /// <summary>
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