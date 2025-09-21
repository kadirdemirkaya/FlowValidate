using FlowValidate.Api.Models;

namespace FlowValidate.Api.Validators
{
    public class UserValidator : BaseValidator<User>
    {
        public UserValidator()
        {
            RuleFor(user => user.PastTime)
                .IsInFuture().WithMessage("PastTime is not in future");

            RuleFor(user => user.Name)
                 .Must(name => !string.IsNullOrEmpty(name)).WithMessage("Name property is null !")
                 .Contains("e").WithMessage("Name property value have to contains 'e' .")
                 .Length(3, 10).WithMessage("Length doesn't follow the rules")
                 .IsEqual("XoarkanX").WithMessage("Name value is not equal qith expected value !")
                 .IsEmail().WithMessage("Name is not email")
                 .Should(name =>
                 {
                     if (!name.Contains("e"))
                         throw new Exception("Name cannot start with 'X'.");

                 }, "Custom validation FAILED !").WithMessage("Additional error message if needed.")
                .Should(name => char.IsUpper(name[0]), "Name must start with an uppercase letter");
        }
    }
}
