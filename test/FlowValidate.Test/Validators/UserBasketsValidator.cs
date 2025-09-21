using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class UserBasketsValidator : BaseValidator<UserBasket>
    {
        public UserBasketsValidator()
        {
            RuleFor(x => x.Name).IsNotEmpty().WithMessage("UserBaskets name is required.").Length(3, 50);
            RuleFor(x => x.Count).IsInRange(1, 100); 
        }
    }
}
