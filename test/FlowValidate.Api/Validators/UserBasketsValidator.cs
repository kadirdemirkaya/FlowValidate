using FlowValidate.Api.Models;

namespace FlowValidate.Api.Validators
{
    public class UserBasketsValidator : BaseValidator<UserBasket>
    {
        public UserBasketsValidator()
        {
            RuleFor(ubas => ubas.Count)
                .IsNotEmpty().WithMessage("[UserBasket] count is null !")
                .IsGreaterThan(0).WithMessage("[UserBasket] count have to be grater then 5");

            RuleFor(ubas => ubas.Name)
                 .IsNotEmpty().WithMessage("[UserBasket] name is null")
                 .Length(1, 10).WithMessage("[UserBasket] name havbe to be between 1 and 10");
        }
    }
}
