using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class UserValidator : BaseValidator<User>
    {
        public UserValidator()
        {
            RuleFor(x => x.Name).IsNotEmpty().WithMessage("Name is empty").Length(3, 50);
            RuleFor(x => x.Age).IsInRange(18, 60);
            RuleFor(x => x.Email).IsEmail().WithMessage("Email is not valid");
            RuleFor(x => x.PastTime).IsDateInPast();
            RuleFor(x => x.Tags).IsUnique().WithMessage("Tags must be unique");

            // Nested validator
            ValidateNested(x => x.UserDetails, new UserDetailsValidator());
            

            // Collection validator
            ValidateCollection(
                u => u.UserBaskets,          
                new UserBasketsValidator(),  
                item => item                 
            );

            // Registry Rules
            RuleFor(u => u.Nickname)
                .Should((nickname, addError) =>
                {
                    if (!string.IsNullOrEmpty(nickname))
                    {
                        if (nickname.Length < 3)
                            addError("Nickname must be at least 3 characters long.");

                        if (nickname.Contains(" "))
                            addError("Nickname cannot contain spaces.");
                    }
                });
        }
    }
}
