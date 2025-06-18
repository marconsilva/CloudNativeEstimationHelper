using System.Text.Json.Serialization;

namespace CNEH_Web.Models
{
    /// <summary>
    /// Response object from the Azure Pricing API
    /// </summary>
    public class AzurePriceResponse
    {
        [JsonPropertyName("BillingCurrency")]
        public string? BillingCurrency { get; set; }

        [JsonPropertyName("CustomerEntityId")]
        public string? CustomerEntityId { get; set; }

        [JsonPropertyName("CustomerEntityType")]
        public string? CustomerEntityType { get; set; }

        [JsonPropertyName("Items")]
        public List<AzurePriceItem>? Items { get; set; }

        [JsonPropertyName("NextPageLink")]
        public string? NextPageLink { get; set; }

        [JsonPropertyName("Count")]
        public int Count { get; set; }
    }
}