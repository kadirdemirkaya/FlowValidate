using FlowValidate.Api.Models;

namespace FlowValidate.Api.Validators
{
    public class UserCustomersValidator : BaseValidator<UserCustomer>
    {
        public UserCustomersValidator()
        {
            RuleFor(product => product.Email)
                .IsNotEmpty().WithMessage("[UserCustomer] Email is null ")
                .IsEmail().WithMessage("[UserCustomer] not Email format");

            RuleFor(product => product.PhoneNumber)
                .IsNotEmpty().WithMessage("[UserCustomer] PhoneNumber is null ")
                .Length(10, 11).WithMessage("[UserCustomer] not PhoneNumber format");
        }
    }
}
