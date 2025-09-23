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
                    result.AddFailure("*** Email boş olamaz ***.");

                else if (!email.Contains("@"))
                    result.AddFailure("*** Email formatı yanlış ***");

                result.SetIsValid(result.Failures.Count == 0);

                return Task.FromResult(result);
            });
        }
    }
}
