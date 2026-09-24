using FlowValidate.Enums;
using FlowValidate.Models;

namespace FlowValidate
{
    /// <summary>
    /// The outcome of running a validator: whether the instance is valid, and the list of
    /// <see cref="ValidationFailure"/>s collected while running its rules.
    /// </summary>
    public class ValidationResult
    {
        private readonly object _sync = new object();

        /// <summary>
        /// When set to <see langword="true"/> by a rule (e.g. <c>RequiredIf</c>), stops any rules
        /// remaining after it in the same property's chain from running.
        /// </summary>
        public bool SkipRemainingRules { get; set; } = false;

        /// <summary>
        /// <see langword="true"/> when no failure has been recorded yet.
        /// </summary>
        public bool IsValid { get; private set; } = true;

        private readonly List<ValidationFailure> _failures = new List<ValidationFailure>();

        /// <summary>
        /// The failures recorded so far, in the order they were added.
        /// </summary>
        public IReadOnlyList<ValidationFailure> Failures => _failures.AsReadOnly();

        /// <summary>
        /// Creates an empty, valid result with no failures.
        /// </summary>
        public ValidationResult() { }

        /// <summary>
        /// Creates a new, valid <see cref="ValidationResult"/> with no failures.
        /// </summary>
        public static ValidationResult Success() => new ValidationResult();

        /// <summary>
        /// Creates a <see cref="ValidationResult"/> that already contains a single failure.
        /// </summary>
        /// <param name="message">The failure's error message.</param>
        /// <param name="propertyName">The name of the property the failure applies to.</param>
        /// <param name="attemptedValue">The value that failed validation.</param>
        /// <param name="errorCode">An optional machine-readable error code.</param>
        /// <param name="severity">The severity of the failure. Defaults to <see cref="Severity.Error"/>.</param>
        /// <returns>A new, invalid <see cref="ValidationResult"/> containing the failure.</returns>
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

        /// <summary>
        /// Sets <see cref="SkipRemainingRules"/> under the internal lock.
        /// </summary>
        /// <param name="skipRemainingRules">The new value.</param>
        public void SetSkipRemainingRules(bool skipRemainingRules)
        {
            lock (_sync)
            {
                SkipRemainingRules = skipRemainingRules;
            }
        }

        /// <summary>
        /// Sets <see cref="IsValid"/> under the internal lock.
        /// </summary>
        /// <param name="isValid">The new value.</param>
        public void SetIsValid(bool isValid)
        {
            lock (_sync)
            {
                IsValid = isValid;
            }
        }

        /// <summary>
        /// Records a failure and marks this result as invalid. A <see langword="null"/> failure is ignored.
        /// </summary>
        /// <param name="failure">The failure to add.</param>
        public void AddFailure(ValidationFailure failure)
        {
            if (failure == null) return;
            lock (_sync)
            {
                IsValid = false;
                _failures.Add(failure);
            }
        }

        /// <summary>
        /// Builds and records a <see cref="ValidationFailure"/> with <see cref="Severity.Error"/>,
        /// and marks this result as invalid.
        /// </summary>
        /// <param name="failure">The failure's error message.</param>
        /// <param name="propertyName">The name of the property the failure applies to.</param>
        /// <param name="attemptedValue">The value that failed validation.</param>
        /// <param name="errorCode">An optional machine-readable error code.</param>
        public void AddFailure(string failure, string? propertyName = null, object? attemptedValue = null, string? errorCode = null)
        {
            if (failure == null) return;
            lock (_sync)
            {
                IsValid = false;
                _failures.Add(new(failure, propertyName, attemptedValue, errorCode, Severity.Error));
            }
        }

        /// <summary>
        /// Merges another result's failures, <see cref="IsValid"/> and <see cref="SkipRemainingRules"/>
        /// state into this one. A <see langword="null"/> <paramref name="other"/> is ignored.
        /// </summary>
        /// <param name="other">The result to merge into this one.</param>
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

        /// <summary>
        /// Groups <see cref="Failures"/> by <see cref="ValidationFailure.PropertyName"/>, in the same
        /// order the failures were originally added (both the key order and, within each key, the
        /// message order). Includes failures of every <see cref="Severity"/>, not only <see cref="Severity.Error"/>.
        /// A <see langword="null"/> or empty <c>PropertyName</c> is grouped under the key
        /// <c>"&lt;root&gt;"</c> (the same sentinel the <see cref="ValidationFailure"/> constructor uses
        /// for a <see langword="null"/> name), so root-level and unnamed failures always end up together.
        /// On a successful (valid) result, returns an empty dictionary.
        /// </summary>
        /// <returns>A dictionary mapping each property name to its error messages, in insertion order.</returns>
        public IDictionary<string, string[]> ToDictionary()
        {
            lock (_sync)
            {
                var result = new Dictionary<string, string[]>();
                foreach (var failure in _failures)
                {
                    var key = string.IsNullOrEmpty(failure.PropertyName) ? "<root>" : failure.PropertyName;
                    if (result.TryGetValue(key, out var existing))
                    {
                        var updated = new string[existing.Length + 1];
                        Array.Copy(existing, updated, existing.Length);
                        updated[existing.Length] = failure.ErrorMessage;
                        result[key] = updated;
                    }
                    else
                    {
                        result[key] = new[] { failure.ErrorMessage };
                    }
                }
                return result;
            }
        }
    }

}
