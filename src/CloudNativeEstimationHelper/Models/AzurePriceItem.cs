using System.Text.Json.Serialization;

namespace CloudNativeEstimationHelper.Models
{
    /// <summary>
    /// Represents an individual Azure price item
    /// </summary>
    public class AzurePriceItem
    {
        [JsonPropertyName("currencyCode")]
        public string? CurrencyCode { get; set; }

        [JsonPropertyName("tierMinimumUnits")]
        public double TierMinimumUnits { get; set; }

        [JsonPropertyName("retailPrice")]
        public double RetailPrice { get; set; }

        [JsonPropertyName("unitPrice")]
        public double UnitPrice { get; set; }

        [JsonPropertyName("armRegionName")]
        public string? ArmRegionName { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("effectiveStartDate")]
        public DateTime EffectiveStartDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public DateTime EffectiveEndDate { get; set; }

        [JsonPropertyName("meterId")]
        public string? MeterId { get; set; }

        [JsonPropertyName("meterName")]
        public string? MeterName { get; set; }

        [JsonPropertyName("productId")]
        public string? ProductId { get; set; }

        [JsonPropertyName("skuId")]
        public string? SkuId { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("skuName")]
        public string? SkuName { get; set; }

        [JsonPropertyName("serviceName")]
        public string? ServiceName { get; set; }

        [JsonPropertyName("serviceId")]
        public string? ServiceId { get; set; }

        [JsonPropertyName("serviceFamily")]
        public string? ServiceFamily { get; set; }

        [JsonPropertyName("unitOfMeasure")]
        public string? UnitOfMeasure { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("isPrimaryMeterRegion")]
        public bool IsPrimaryMeterRegion { get; set; }

        [JsonPropertyName("armSkuName")]
        public string? ArmSkuName { get; set; }

        [JsonPropertyName("savingsPlan")]
        public List<SavingsPlan> SavingsPlan { get; set; }
        [JsonPropertyName("reservationTerm")]
        public string ReservationTerm { get; set; }
    
    }
}