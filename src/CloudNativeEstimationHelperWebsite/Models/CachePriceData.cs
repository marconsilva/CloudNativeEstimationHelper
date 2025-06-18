using System.Text.Json.Serialization;

namespace CNEH_Web.Models
{ 
    /// <summary>
    /// Class to hold both the cached data and metadata
    /// </summary>
    public class CachePriceData
    {
        [JsonPropertyName("metadata")]
        public CacheMetadata Metadata { get; set; } = new CacheMetadata();
        
        [JsonPropertyName("pricesByService")]
        public Dictionary<string, List<AzurePriceItem>> PricesByService { get; set; } = new();
    }
}