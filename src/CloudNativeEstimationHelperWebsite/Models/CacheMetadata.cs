using System.Text.Json.Serialization;

namespace CNEH_Web.Models
{ 
    /// <summary>
    /// Metadata about the cached prices
    /// </summary>
    public class CacheMetadata
    {
        [JsonPropertyName("currencyCode")]
        public string CurrencyCode { get; set; } = "USD";
        
        [JsonPropertyName("filterByRegion")]
        public bool FilterByRegion { get; set; }
        
        [JsonPropertyName("configuredRegions")]
        public List<string> ConfiguredRegions { get; set; } = new();
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}