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

        public ValidationResult Validate(T instance)
        {
            var result = new ValidationResult();
            var value = _property(instance);

            var baseValidationResult = _validator.Validate(value);
            if (!baseValidationResult.IsValid)
            {
                result.Errors.AddRange(baseValidationResult.Errors);
            }

            result.SetIsValid(result.Errors.Count == 0);

            return result;
        }
    }

}
