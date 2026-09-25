namespace FlowValidate.Test.Models
{
    public class CustomerAddress
    {
        public string City { get; set; } = string.Empty;
    }

    public class Customer
    {
        public CustomerAddress? Billing { get; set; }
    }
}
