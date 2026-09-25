namespace FlowValidate.Rules
{
    public class ValidationRegistryRules<T, TProperty>
    {
        private readonly Func<T, TProperty> _property;
        private readonly BaseValidator<TProperty> _validator;

        public ValidationRegistryRules(Func<T, TProperty> property, BaseValidator<TProperty> validator)
        {
            _property = property;
            _validator = validator;
        }


        public Task<ValidationResult> ValidateAsync(T instance) => ValidateAsync(instance, CancellationToken.None);

        public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken)
        {
            var result = new ValidationResult();
            var value = _property(instance);

            if (value == null)
                return result;

            var baseValidationResult = await _validator.ValidateAsync(value, cancellationToken);

            if (!baseValidationResult.IsValid) result.Merge(baseValidationResult);

            return result;
        }

    }
}
