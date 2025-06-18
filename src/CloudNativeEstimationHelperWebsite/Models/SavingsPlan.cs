using System.Text.Json.Serialization;
namespace CNEH_Web.Models
{
    public class SavingsPlan
    {
        [JsonPropertyName("unitPrice")]
        public double? UnitPrice { get; set; }
        [JsonPropertyName("retailPrice")]
        public double? RetailPrice { get; set; }
        [JsonPropertyName("term")]
        public string? Term { get; set; }
    }
}
