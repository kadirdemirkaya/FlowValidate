using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowValidate.Console.Validators
{
    public class UserNameValidator : BaseValidator<string>
    {
        public UserNameValidator()
        {
            _rules.Add(name =>
            {
                var result = new ValidationResult();

                if (string.IsNullOrWhiteSpace(name))
                    result.AddFailure("*** Username cannot be empty ***.");
                else
                {
                    if (name.Length < 3 || name.Length > 20)
                        result.AddFailure("Username must be between 3 and 20 characters.",errorCode: "UserNameValidator");
                    if (name.Contains(" "))
                        result.AddFailure("Username cannot contain spaces", errorCode: "UserNameValidator");
                    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_]+$"))
                        result.AddFailure("Username can only contain letters, numbers and underscores.", errorCode: "UserNameValidator");
                    if (!char.IsLetterOrDigit(name[0]))
                        result.AddFailure("Username must start with a letter or number.", errorCode: "UserNameValidator");
                }
                result.SetIsValid(result.Failures.Count == 0);
                return Task.FromResult(result);
            });

        }
    }
}
