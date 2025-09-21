namespace FlowValidate.Console.Validators
{
    public class EmailValidator : BaseValidator<string>
    {
        public EmailValidator()
        {
            _rules.Add(email =>
            {
                var result = new ValidationResult();

                if (string.IsNullOrWhiteSpace(email))
                    result.Errors.Add("*** Email boş olamaz ***.");

                else if (!email.Contains("@"))
                    result.Errors.Add("*** Email formatı yanlış ***");

                result.SetIsValid(result.Errors.Count == 0);
                return result;
            });
        }
    }
}
