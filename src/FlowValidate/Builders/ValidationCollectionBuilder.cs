using FlowValidate.Models;
using System.Text.RegularExpressions;

namespace FlowValidate.Builders
{
    public class ValidationCollectionBuilder<T, TCollection, TElement>
    {
        private readonly Func<T, IEnumerable<TCollection>> _collectionFunc;
        private readonly BaseValidator<TElement> _elementValidator;
        private readonly Func<TCollection, TElement> _itemSelector;

        public ValidationCollectionBuilder(
           Func<T, IEnumerable<TCollection>> collectionFunc,
           BaseValidator<TElement> elementValidator,
           Func<TCollection, TElement> itemSelector)
        {
            _collectionFunc = collectionFunc;
            _elementValidator = elementValidator;
            _itemSelector = itemSelector;
        }

        public async Task<ValidationResult> ValidateAsync(T instance)
        {
            var result = new ValidationResult();
            var collection = _collectionFunc(instance);
            int count = 1;

            foreach (var item in collection)
            {
                var element = _itemSelector(item);
                var itemResult = await _elementValidator.ValidateAsync(element);

                if (!itemResult.IsValid)
                    foreach (var failure in itemResult.Failures)
                        result.AddFailure(new ValidationFailure(
                            propertyName: failure.PropertyName,
                            errorMessage: $"Element {count}: {failure.ErrorMessage}",
                            attemptedValue: failure.AttemptedValue,
                            errorCode: failure.ErrorCode,
                            severity: failure.Severity
                        ));

                count++;
            }

            return result;
        }
    }
}