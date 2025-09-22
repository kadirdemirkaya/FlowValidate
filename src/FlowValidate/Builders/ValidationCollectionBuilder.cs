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
                {
                    result.Errors.AddRange(itemResult.Errors.Select(err => $"Element {count}: {err}"));
                    result.SetIsValid(false);
                }

                count++;
            }

            return result;
        }

    }
}