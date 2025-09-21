using FlowValidate.Abstractions;
using FlowValidate.Builders;
using FlowValidate.Rules;

namespace FlowValidate
{
    public abstract class BaseValidator<T> : IBaseValidator<T>
    {
        protected readonly List<Func<T, ValidationResult>> _rules = new();

        public ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> property)
        {
            var builder = new ValidationRuleBuilder<T, TProperty>(property);
            _rules.Add(instance => builder.Validate(instance));
            return builder;
        }

        public ValidationNestedBuilder<T, TProperty> ValidateNested<TProperty>(
            Func<T, TProperty> propertyFunc,
            BaseValidator<TProperty> validator)
        {
            var builder = new ValidationNestedBuilder<T, TProperty>(propertyFunc, validator);

            _rules.Add(instance =>
            {
                var nestedObj = propertyFunc(instance);

                if (nestedObj == null)
                    return new ValidationResult();

                return builder.Validate(instance);
            });

            return builder;
        }


        public ValidationCollectionBuilder<T, TCollection, TElement> ValidateCollection<TCollection, TElement>(
            Func<T, IEnumerable<TCollection>> collectionFunc,
            BaseValidator<TElement> elementValidator,
            Func<TCollection, TElement> itemSelector)
        {
            var builder = new ValidationCollectionBuilder<T, TCollection, TElement>(collectionFunc, elementValidator, itemSelector);
            _rules.Add(instance => builder.Validate(instance));
            return builder;
        }
        public ValidationRegistryRules<T, TProperty> ValidateRegistryRules<TProperty>(Func<T, TProperty> propertyFunc, BaseValidator<TProperty> validator)
        {
            var builder = new ValidationRegistryRules<T, TProperty>(propertyFunc, validator);
            _rules.Add(instance => builder.Validate(instance));
            return builder;
        }

        public ValidationResult Validate(T instance)
        {
            var result = new ValidationResult();

            if (_rules.Count() > 0)
                foreach (var rule in _rules)
                {
                    var ruleResult = rule(instance);
                    if (!ruleResult.IsValid)
                    {
                        result.SetIsValid(false);
                        result.Errors.AddRange(ruleResult.Errors);
                    }
                }

            return result;
        }
    }
}
