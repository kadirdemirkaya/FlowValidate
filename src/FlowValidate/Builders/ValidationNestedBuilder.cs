using System.ComponentModel.DataAnnotations;

namespace FlowValidate.Builders
{
    public class ValidationNestedBuilder<T, TProperty>
    {
        private readonly Func<T, TProperty> _property;
        private readonly BaseValidator<TProperty> _validator;

        public ValidationNestedBuilder(Func<T, TProperty> property, BaseValidator<TProperty> validator)
        {
            _property = property;
            _validator = validator;
        }

        public async Task<ValidationResult> ValidateAsync(T instance)
        {
            var result = new ValidationResult();
            var value = _property(instance);

            var baseValidationResult = await _validator.ValidateAsync(value);

            if (!baseValidationResult.IsValid) result.Merge(baseValidationResult);

            return result;
        }

    }

}
