using FlowValidate.Console.Models;

namespace FlowValidate.Console.Validators
{
    public class UserValidator : BaseValidator<User>
    {
        public UserValidator()
        {
            RuleFor(user => user.Name)
                 .Must(name => !string.IsNullOrEmpty(name)).WithMessage("Name property is null !")
                 .Contains("e").WithMessage("Name property value have to contains 'e' .")
                 .Length(3, 10).WithMessage("Length doesn't follow the rules")
                 .RequiredIf(name => name.Length > 300)
                 .IsEqual("XoarkanX").WithMessage("Name value is not equal qith expected value !")
                 .IsEmail().WithMessage("Name is not email")
                 .Should(name =>
                 {
                     if (!name.Contains("e"))
                         throw new Exception("Name cannot start with 'X'.");

                 }, "Custom validation FAILED !").WithMessage("Additional error message if needed.")
                .Should(name => char.IsUpper(name[0]), "Name must start with an uppercase letter");

            ValidateRegistryRules(user => user.Name, new UserNameValidator());

            ValidateNested(user => user.UserCustomer, new UserCustomersValidator());

            RuleFor(user => user.Age)
                .Length(1, 2).WithMessage("Age range can't be bigger than three step")
                .IsEqual(17).WithMessage("yas 17 olmali");

            ValidateCollection(
                ubas => ubas.UserBaskets,
                new UserBasketsValidator(),
                item => item
            );

            RuleFor(user => user.Emails)
                .Should(emails =>
                {
                    foreach (var email in emails)
                    {
                        if (email.Contains("o"))
                            throw new Exception("Email format is wrong");
                        if (email is null)
                            throw new Exception("Email is null");
                    }
                }, "Email format is wrong or is null");

            //RuleFor(user => user.Name)
            //   .Should((name, addError) =>
            //   {
            //       if (string.IsNullOrEmpty(name))
            //       {
            //           addError("Name cannot be null or empty.");
            //       }
            //       if (name.Length < 3)
            //       {
            //           addError("Name must be at least 3 characters long.");
            //       }
            //       if (!name.Contains("e"))
            //       {
            //           addError("Email is must be contains 'e' character !");
            //       }
            //   });

            //RuleFor(user => user.Name)
            //   .Should((name) =>
            //   {
            //       if (string.IsNullOrEmpty(name))
            //       {
            //           throw new Exception();
            //       }
            //       if (name.Length < 3)
            //       {
            //           throw new Exception();
            //       }
            //       if (!name.Contains("e"))
            //       {
            //           throw new Exception();
            //       }
            //   }, "null error", "length error", "e contains error");

            //RuleFor(user => user.Age)
            //    .Should(age =>
            //    {
            //        throw new Exception();
            //    }, "example error");

            //RuleFor(user => user)
            //    .Must(user =>
            //    {
            //        if (!user.Name.Contains("y"))
            //            return false;
            //        return true;
            //    }).WithMessage("user name must be contains 'y' character !")
            //    .Must(user =>
            //    {
            //        if (user.Name.Length < 5)
            //            return false;
            //        return true;
            //    }).WithMessage("user name length at least be than 3");
        }
    }
}
