using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class UserDetailsValidator : BaseValidator<UserDetails>
    {
        public UserDetailsValidator()
        {
            RuleFor(x => x.Address).IsNotEmpty().WithMessage("UserDetails address is required.").Length(5, 100);
            RuleFor(x => x.Phone).MatchesRegex(@"^\d{10}$");
        }
    }
}
