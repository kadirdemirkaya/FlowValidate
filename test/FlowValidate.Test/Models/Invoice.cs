using System.Text.Json.Serialization;

namespace FlowValidate.Test.Models
{
    public class Invoice
    {
        [JsonPropertyName("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("total_amount")]
        public int TotalAmount { get; set; }
    }
}
