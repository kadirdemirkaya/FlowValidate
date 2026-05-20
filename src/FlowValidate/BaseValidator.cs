using FlowValidate.Abstractions;
using FlowValidate.Builders;
using FlowValidate.Rules;
using System.Linq.Expressions;

namespace FlowValidate
{
    public abstract class BaseValidator<T> : IBaseValidator<T>
    {
        protected readonly List<Func<T, Task<ValidationResult>>> _rules = new();
        protected readonly List<Func<bool>> _asyncCheckers = new();

        public bool HasAsyncRules => _asyncCheckers.Any(check => check());

        public ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> property)
        {
            var builder = new ValidationRuleBuilder<T, TProperty>(property);
            _rules.Add(async instance => await builder.ValidateAsync(instance));
            _asyncCheckers.Add(() => builder.HasAsyncRules);
            return builder;
        }

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

        public ValidationRegistryRules<T, TProperty> ValidateRegistryRules<TProperty>(
            Func<T, TProperty> propertyFunc,
            BaseValidator<TProperty> validator)
        {
            var builder = new ValidationRegistryRules<T, TProperty>(propertyFunc, validator);

            _rules.Add(async instance => await builder.ValidateAsync(instance));
            _asyncCheckers.Add(() => validator.HasAsyncRules);

            return builder;
        }

        public ValidationResult Validate(T instance)
        {
            if (HasAsyncRules)
            {
                throw new InvalidOperationException("This validator contains asynchronous rules (e.g., MustAsync or ShouldAsync) and cannot be executed synchronously via Validate(). Please use ValidateAsync() instead to ensure safe, non-blocking asynchronous execution.");
            }
            return ValidateAsync(instance).GetAwaiter().GetResult();
        }

        public async Task<ValidationResult> ValidateAsync(T instance)
        {
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
