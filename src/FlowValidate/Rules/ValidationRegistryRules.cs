using FlowValidate.Models;

namespace FlowValidate.Rules
{
    public class ValidationRegistryRules<T, TProperty>
    {
        private const string RootPropertyName = "<root>";

        private readonly Func<T, TProperty> _property;
        private readonly BaseValidator<TProperty> _validator;
        private string? _propertyPrefix;

        public ValidationRegistryRules(Func<T, TProperty> property, BaseValidator<TProperty> validator)
        {
            _property = property;
            _validator = validator;
        }

        /// <summary>
        /// Opts this registry validator into a property-name prefix: every failure reported by the
        /// registered validator is re-published as <c>"&lt;prefix&gt;.&lt;property&gt;"</c> (e.g.
        /// <c>Billing.City</c>), so a client can tell which property this reusable validator was
        /// applied to. The error message is not affected.
        /// </summary>
        /// <param name="prefix">The prefix the property names should be reported under, e.g. <c>"Billing"</c>.</param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="ArgumentException"><paramref name="prefix"/> is <see langword="null"/>, empty or whitespace.</exception>
        /// <remarks>
        /// Opt-in: without this call the property names and messages stay exactly as before. Nesting
        /// composes with <see cref="Builders.ValidationCollectionBuilder{T, TCollection, TElement}.WithIndexedPropertyNames(string)"/> —
        /// a collection inside a registry-validated property, or the reverse, that both opt in produce
        /// paths such as <c>Orders[0].Address.City</c>.
        /// </remarks>
        public ValidationRegistryRules<T, TProperty> WithPropertyPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("The property prefix must be a non-empty string.", nameof(prefix));

            _propertyPrefix = prefix;

            return this;
        }

        public Task<ValidationResult> ValidateAsync(T instance) => ValidateAsync(instance, CancellationToken.None);

        public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken)
        {
            var result = new ValidationResult();
            var value = _property(instance);

            if (value == null)
                return result;

            var baseValidationResult = await _validator.ValidateAsync(value, cancellationToken);

            if (!baseValidationResult.IsValid)
            {
                if (_propertyPrefix == null)
                {
                    result.Merge(baseValidationResult);
                }
                else
                {
                    foreach (var failure in baseValidationResult.Failures)
                        result.AddFailure(new ValidationFailure(
                            propertyName: BuildPropertyName(failure.PropertyName),
                            errorMessage: failure.ErrorMessage,
                            attemptedValue: failure.AttemptedValue,
                            errorCode: failure.ErrorCode,
                            severity: failure.Severity
                        ));
                }
            }

            return result;
        }

        private string BuildPropertyName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName == RootPropertyName)
                return _propertyPrefix!;

            return $"{_propertyPrefix}.{propertyName}";
        }

    }
}
