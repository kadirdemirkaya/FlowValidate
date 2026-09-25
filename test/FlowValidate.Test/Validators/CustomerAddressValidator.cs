using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class CustomerAddressValidator : BaseValidator<CustomerAddress>
    {
        public CustomerAddressValidator()
        {
            RuleFor(x => x.City).IsNotEmpty().WithMessage("City is required.", "CUSTOMER_ADDRESS_CITY_REQUIRED");
        }
    }
}
