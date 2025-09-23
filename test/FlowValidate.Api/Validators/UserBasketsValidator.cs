using FlowValidate.Api.Models;

namespace FlowValidate.Api.Validators
{
    public class UserBasketsValidator : BaseValidator<UserBasket>
    {
        public UserBasketsValidator()
        {
            RuleFor(ubas => ubas.Count)
                .IsNotEmpty().WithMessage("Count is null !")
                .IsGreaterThan(0).WithMessage("Count have to be grater then 5");

            RuleFor(ubas => ubas.Name)
                 .IsNotEmpty().WithMessage("Name is null")
                 .Length(1, 10).WithMessage("Name havbe to be between 1 and 10");
        }
    }
}
