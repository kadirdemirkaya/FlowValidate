using FlowValidate.Models;
using System.Text.RegularExpressions;

namespace FlowValidate.Builders
{
    public class ValidationCollectionBuilder<T, TCollection, TElement>
    {
        private const string RootPropertyName = "<root>";

        private readonly Func<T, IEnumerable<TCollection>> _collectionFunc;
        private readonly BaseValidator<TElement> _elementValidator;
        private readonly Func<TCollection, TElement> _itemSelector;
        private string? _indexedCollectionName;

        public ValidationCollectionBuilder(
           Func<T, IEnumerable<TCollection>> collectionFunc,
           BaseValidator<TElement> elementValidator,
           Func<TCollection, TElement> itemSelector)
        {
            _collectionFunc = collectionFunc;
            _elementValidator = elementValidator;
            _itemSelector = itemSelector;
        }

        /// <summary>
        /// Opts this collection into indexed property names: every failure reported by the element
        /// validator is re-published as <c>"&lt;collectionName&gt;[&lt;zero-based index&gt;].&lt;property&gt;"</c>
        /// (e.g. <c>Items[1].Name</c>), so a client can tell programmatically which element failed.
        /// The error message is not affected and keeps its one-based <c>"Element 1: ..."</c> prefix.
        /// </summary>
        /// <param name="collectionName">
        /// The name the collection should be reported under, e.g. <c>"Items"</c>. It is passed
        /// explicitly because <see cref="BaseValidator{T}.ValidateCollection{TCollection, TElement}"/>
        /// takes a delegate, not an expression, so the name cannot be inferred from the selector.
        /// </param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="ArgumentException"><paramref name="collectionName"/> is <see langword="null"/>, empty or whitespace.</exception>
        /// <remarks>
        /// Opt-in: without this call the property names and messages stay exactly as before. Nesting
        /// composes — a collection inside a collection that both opt in produces paths such as
        /// <c>Orders[0].Lines[2].Qty</c>.
        /// </remarks>
        public ValidationCollectionBuilder<T, TCollection, TElement> WithIndexedPropertyNames(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("The collection name must be a non-empty string.", nameof(collectionName));

            _indexedCollectionName = collectionName;

            return this;
        }

        public Task<ValidationResult> ValidateAsync(T instance) => ValidateAsync(instance, CancellationToken.None);

        public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken)
        {
            var result = new ValidationResult();
            var collection = _collectionFunc(instance);

            if (collection == null)
                return result;

            int count = 1;

            foreach (var item in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var element = _itemSelector(item);
                var itemResult = await _elementValidator.ValidateAsync(element, cancellationToken);

                if (!itemResult.IsValid)
                    foreach (var failure in itemResult.Failures)
                        result.AddFailure(new ValidationFailure(
                            propertyName: BuildPropertyName(count - 1, failure.PropertyName),
                            errorMessage: $"Element {count}: {failure.ErrorMessage}",
                            attemptedValue: failure.AttemptedValue,
                            errorCode: failure.ErrorCode,
                            severity: failure.Severity
                        ));

                count++;
            }

            return result;
        }

        private string BuildPropertyName(int index, string propertyName)
        {
            if (_indexedCollectionName == null)
                return propertyName;

            var elementName = $"{_indexedCollectionName}[{index}]";

            if (string.IsNullOrEmpty(propertyName) || propertyName == RootPropertyName)
                return elementName;

            return $"{elementName}.{propertyName}";
        }
    }
}