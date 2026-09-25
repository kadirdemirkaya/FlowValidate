using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class CustomerValidator : BaseValidator<Customer>
    {
        public CustomerValidator()
        {
            ValidateNested(x => x.Billing, new CustomerAddressValidator())
                .WithPropertyPrefix("Billing");
        }
    }
}
