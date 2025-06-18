namespace CNEH_Web.Models
{
    /// <summary>
    /// Represents an Azure region for filtering prices
    /// </summary>
    public class AzureRegion
    {
        /// <summary>
        /// The region code (e.g., "westeurope")
        /// </summary>
        public string Code { get; set; } = string.Empty;
        
        /// <summary>
        /// The display name of the region (e.g., "West Europe")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the region is selected for filtering
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Creates a new AzureRegion with the specified code and display name
        /// </summary>
        public AzureRegion(string code, string displayName, bool isSelected = false)
        {
            Code = code;
            DisplayName = displayName;
            IsSelected = isSelected;
        }
    }
}
