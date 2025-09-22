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
                    result.Errors.Add("*** Username cannot be empty ***.");
                else
                {
                    if (name.Length < 3 || name.Length > 20)
                        result.Errors.Add("*** Username must be between 3 and 20 characters. ***.");
                    if (name.Contains(" "))
                        result.Errors.Add("*** Username cannot contain spaces ***.");
                    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_]+$"))
                        result.Errors.Add("*** Username can only contain letters, numbers and underscores ***.");
                    if (!char.IsLetterOrDigit(name[0]))
                        result.Errors.Add("*** Username must start with a letter or number ***.");
                }
                result.SetIsValid(result.Errors.Count == 0);
                return Task.FromResult(result);
            });

        }
    }
}
