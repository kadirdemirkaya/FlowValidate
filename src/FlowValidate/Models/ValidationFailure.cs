using FlowValidate.Enums;

namespace FlowValidate.Models
{
    public class ValidationFailure
    {
        public string PropertyName { get; }
        public object? AttemptedValue { get; }
        public string ErrorMessage { get; }
        public string? ErrorCode { get; }
        public Severity Severity { get; }

        public ValidationFailure(string errorMessage,
                                 string? propertyName = null,
                                 object? attemptedValue = null,
                                 string? errorCode = null,
                                 Severity severity = Severity.Error)
        {
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            PropertyName = propertyName;
            AttemptedValue = attemptedValue;
            ErrorCode = errorCode;
            Severity = severity;
        }

        public override string ToString()
        {
            var propertyPart = PropertyName ?? "<root>";
            var codePart = string.IsNullOrEmpty(ErrorCode) ? "" : $" - [{ErrorCode}]";
            return $"{propertyPart}{codePart}: {ErrorMessage}";
        }

    }
}
