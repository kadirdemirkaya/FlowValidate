namespace FlowValidate
{
    public class ValidationResult
    {
        public bool SkipRemainingRules { get; set; } = false;
        public bool IsValid { get; private set; } = true;
        public List<string> Errors { get; private set; } = new List<string>();

        public void SetSkipRemainingRules(bool skipRemainingRules)
        {
            SkipRemainingRules = skipRemainingRules;
        }

        public void SetIsValid(bool isValid)
        {
            IsValid = isValid;
        }

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }

        public void AddError(List<string> errors)
        {
            IsValid = false;
            Errors.AddRange(errors);
        }
    }
}
