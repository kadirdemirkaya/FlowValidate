using FlowValidate.Api.Models;

namespace FlowValidate.Api.Validators
{
    public class UserCustomersValidator : BaseValidator<UserCustomer>
    {
        public UserCustomersValidator()
        {
            RuleFor(product => product.Email)
                .IsNotEmpty().WithMessage("Email is null ")
                .IsEmail().WithMessage("Emil format is not correct.");

            RuleFor(product => product.PhoneNumber)
                .IsNotEmpty().WithMessage("PhoneNumber is null ")
                .Length(10, 11).WithMessage("PhoneNumber format is not corrrect");
        }
    }
}
