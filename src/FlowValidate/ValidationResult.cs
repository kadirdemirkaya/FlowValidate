using FlowValidate.Enums;
using FlowValidate.Models;

namespace FlowValidate
{
    public class ValidationResult
    {
        private readonly object _sync = new object();

        public bool SkipRemainingRules { get; set; } = false;
        public bool IsValid { get; private set; } = true;

        private readonly List<ValidationFailure> _failures = new List<ValidationFailure>();
        public IReadOnlyList<ValidationFailure> Failures => _failures.AsReadOnly();

        public ValidationResult() { }

        public static ValidationResult Success() => new ValidationResult();

        public static ValidationResult Failure(string message,
                                               string? propertyName = null,
                                               object? attemptedValue = null,
                                               string? errorCode = null,
                                               Severity severity = Severity.Error)
        {
            var r = new ValidationResult();
            r.AddFailure(new ValidationFailure(message, propertyName, attemptedValue, errorCode, severity));
            return r;
        }

        public void SetSkipRemainingRules(bool skipRemainingRules)
        {
            lock (_sync)
            {
                SkipRemainingRules = skipRemainingRules;
            }
        }

        public void SetIsValid(bool isValid)
        {
            lock (_sync)
            {
                IsValid = isValid;
            }
        }

        public void AddFailure(ValidationFailure failure)
        {
            if (failure == null) return;
            lock (_sync)
            {
                IsValid = false;
                _failures.Add(failure);
            }
        }

        public void AddFailure(string failure, string? propertyName = null, object? attemptedValue = null, string? errorCode = null)
        {
            if (failure == null) return;
            lock (_sync)
            {
                IsValid = false;
                _failures.Add(new(failure, propertyName, attemptedValue, errorCode, Severity.Error));
            }
        }

        public void Merge(ValidationResult other)
        {
            if (other == null) return;
            lock (_sync)
            {
                if (!other.IsValid) IsValid = false;
                if (other.SkipRemainingRules) SkipRemainingRules = true;

                if (other._failures.Count > 0)
                    _failures.AddRange(other._failures);
            }
        }

        public override string ToString()
        {
            if (IsValid) return "Success";
            lock (_sync)
            {
                return string.Join("; ", _failures.Select(f => f.ToString()));
            }
        }
    }

}
