using FlowValidate.Enums;

namespace FlowValidate.Models
{
    /// <summary>
    /// A single validation error: which property failed, what value was attempted, and why.
    /// </summary>
    public class ValidationFailure
    {
        /// <summary>
        /// The name of the property this failure applies to, or <c>"&lt;root&gt;"</c> when none was given.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// The value that failed validation, if available.
        /// </summary>
        public object? AttemptedValue { get; }

        /// <summary>
        /// A human-readable description of the failure.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// An optional machine-readable error code.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// The severity of the failure.
        /// </summary>
        public Severity Severity { get; }

        /// <summary>
        /// Creates a new <see cref="ValidationFailure"/>.
        /// </summary>
        /// <param name="errorMessage">A human-readable description of the failure.</param>
        /// <param name="propertyName">The name of the property this failure applies to.</param>
        /// <param name="attemptedValue">The value that failed validation.</param>
        /// <param name="errorCode">An optional machine-readable error code.</param>
        /// <param name="severity">The severity of the failure. Defaults to <see cref="Severity.Error"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="errorMessage"/> is <see langword="null"/>.</exception>
        public ValidationFailure(string errorMessage,
                                 string? propertyName = null,
                                 object? attemptedValue = null,
                                 string? errorCode = null,
                                 Severity severity = Severity.Error)
        {
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            PropertyName = propertyName ?? "<root>";
            AttemptedValue = attemptedValue;
            ErrorCode = errorCode;
            Severity = severity;
        }

        /// <summary>
        /// Formats the failure as <c>"PropertyName - [ErrorCode]: ErrorMessage"</c> (the error code part is omitted when absent).
        /// </summary>
        public override string ToString()
        {
            var codePart = string.IsNullOrEmpty(ErrorCode) ? "" : $" - [{ErrorCode}]";
            return $"{PropertyName}{codePart}: {ErrorMessage}";
        }

    }
}
