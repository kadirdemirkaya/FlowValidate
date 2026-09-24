using FlowValidate.Abstractions;
using FlowValidate.Builders;
using FlowValidate.Rules;
using System.Linq.Expressions;

namespace FlowValidate
{
    /// <summary>
    /// Base class for building fluent validators for <typeparamref name="T"/>. Derive from this
    /// type and register rules in the constructor using <see cref="RuleFor{TProperty}"/>,
    /// <see cref="ValidateNested{TProperty}"/>, <see cref="ValidateCollection{TCollection, TElement}"/>
    /// or <see cref="ValidateRegistryRules{TProperty}"/>, then call <see cref="Validate"/> or
    /// <see cref="ValidateAsync"/> to run them.
    /// </summary>
    /// <typeparam name="T">The type of the instance being validated.</typeparam>
    public abstract class BaseValidator<T> : IBaseValidator<T>
    {
        protected readonly List<Func<T, Task<ValidationResult>>> _rules = new();
        protected readonly List<Func<bool>> _asyncCheckers = new();

        /// <summary>
        /// <see langword="true"/> if any registered rule (including rules of nested, collection or
        /// registry validators) is asynchronous. When <see langword="true"/>, <see cref="Validate"/>
        /// throws and <see cref="ValidateAsync"/> must be used instead.
        /// </summary>
        public bool HasAsyncRules => _asyncCheckers.Any(check => check());

        /// <summary>
        /// Starts a fluent rule chain for a single property of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property being validated.</typeparam>
        /// <param name="property">An expression selecting the property, e.g. <c>x => x.Name</c>.</param>
        /// <returns>A builder used to attach rules to the selected property.</returns>
        public ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> property)
        {
            var builder = new ValidationRuleBuilder<T, TProperty>(property);
            _rules.Add(async instance => await builder.ValidateAsync(instance));
            _asyncCheckers.Add(() => builder.HasAsyncRules);
            return builder;
        }

        /// <summary>
        /// Validates a nested object with a separate <see cref="BaseValidator{TProperty}"/>. A
        /// <see langword="null"/> nested object is treated as valid and the nested validator is skipped.
        /// </summary>
        /// <typeparam name="TProperty">The type of the nested object.</typeparam>
        /// <param name="propertyFunc">Selects the nested object from the parent instance.</param>
        /// <param name="validator">The validator used to validate the nested object.</param>
        /// <returns>A builder wrapping the nested validation rule.</returns>
        public ValidationNestedBuilder<T, TProperty> ValidateNested<TProperty>(
            Func<T, TProperty?> propertyFunc,
            BaseValidator<TProperty> validator)
        {
            var builder = new ValidationNestedBuilder<T, TProperty>(propertyFunc, validator);

            _rules.Add(async instance =>
            {
                var nestedObj = propertyFunc(instance);

                if (nestedObj == null)
                    return new ValidationResult();

                return await builder.ValidateAsync(instance);
            });

            _asyncCheckers.Add(() => validator.HasAsyncRules);

            return builder;
        }

        /// <summary>
        /// Validates each element of a collection property with a per-element
        /// <see cref="BaseValidator{TElement}"/>. Failures are prefixed with the element's index
        /// (e.g. <c>"Element 1: ..."</c>). A <see langword="null"/> collection is treated as valid.
        /// </summary>
        /// <typeparam name="TCollection">The type of the items yielded by the collection.</typeparam>
        /// <typeparam name="TElement">The type validated by <paramref name="elementValidator"/>.</typeparam>
        /// <param name="collectionFunc">Selects the collection from the parent instance.</param>
        /// <param name="elementValidator">The validator applied to each selected element.</param>
        /// <param name="itemSelector">Projects each collection item into the type the validator expects.</param>
        /// <returns>
        /// A builder wrapping the collection validation rule. Call
        /// <see cref="ValidationCollectionBuilder{T, TCollection, TElement}.WithIndexedPropertyNames(string)"/>
        /// on it to report each failure under an indexed property name such as <c>Items[1].Name</c>.
        /// </returns>
        public ValidationCollectionBuilder<T, TCollection, TElement> ValidateCollection<TCollection, TElement>(
            Func<T, IEnumerable<TCollection>> collectionFunc,
            BaseValidator<TElement> elementValidator,
            Func<TCollection, TElement> itemSelector)
        {
            var builder = new ValidationCollectionBuilder<T, TCollection, TElement>(collectionFunc, elementValidator, itemSelector);

            _rules.Add(async instance => await builder.ValidateAsync(instance));
            _asyncCheckers.Add(() => elementValidator.HasAsyncRules);

            return builder;
        }

        /// <summary>
        /// Applies a reusable, pre-configured <see cref="BaseValidator{TProperty}"/> to a single
        /// property, so common rule sets (e.g. an address validator) can be shared across parents.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property being validated.</typeparam>
        /// <param name="propertyFunc">Selects the property from the parent instance.</param>
        /// <param name="validator">The reusable validator applied to the selected property.</param>
        /// <returns>A builder wrapping the registry validation rule.</returns>
        public ValidationRegistryRules<T, TProperty> ValidateRegistryRules<TProperty>(
            Func<T, TProperty> propertyFunc,
            BaseValidator<TProperty> validator)
        {
            var builder = new ValidationRegistryRules<T, TProperty>(propertyFunc, validator);

            _rules.Add(async instance => await builder.ValidateAsync(instance));
            _asyncCheckers.Add(() => validator.HasAsyncRules);

            return builder;
        }

        /// <summary>
        /// Runs all registered rules synchronously and returns the aggregated result.
        /// </summary>
        /// <param name="instance">The instance to validate. Must not be <see langword="null"/>.</param>
        /// <returns>The aggregated <see cref="ValidationResult"/> across all registered rules.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="HasAsyncRules"/> is <see langword="true"/>; use <see cref="ValidateAsync"/> instead.
        /// </exception>
        public ValidationResult Validate(T instance)
        {
            if (HasAsyncRules)
            {
                throw new InvalidOperationException("This validator contains asynchronous rules (e.g., MustAsync or ShouldAsync) and cannot be executed synchronously via Validate(). Please use ValidateAsync() instead to ensure safe, non-blocking asynchronous execution.");
            }
            return ValidateAsync(instance).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs all registered rules asynchronously and returns the aggregated result.
        /// </summary>
        /// <param name="instance">The instance to validate. Must not be <see langword="null"/>.</param>
        /// <returns>The aggregated <see cref="ValidationResult"/> across all registered rules.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        public async Task<ValidationResult> ValidateAsync(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance), "The instance to validate cannot be null.");

            var result = new ValidationResult();

            foreach (var rule in _rules)
            {
                var ruleResult = await rule(instance);

                if (!ruleResult.IsValid) result.Merge(ruleResult);
            }

            return result;
        }

    }
}
