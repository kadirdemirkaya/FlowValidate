using FlowValidate;
using FlowValidate.Builders;
using FlowValidate.Rules;

namespace FlowValidate.Abstractions
{
    public interface IBaseValidator<T>
    {
        ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> property);

        ValidationResult Validate(T instance);

        ValidationNestedBuilder<T, TProperty> ValidateNested<TProperty>(Func<T, TProperty> propertyFunc, BaseValidator<TProperty> validator);

        ValidationCollectionBuilder<T, TCollection, TElement> ValidateCollection<TCollection, TElement>(
              Func<T, IEnumerable<TCollection>> collectionFunc,
              BaseValidator<TElement> elementValidator,
              Func<TCollection, TElement> itemSelector);

        ValidationRegistryRules<T, TProperty> ValidateRegistryRules<TProperty>(Func<T, TProperty> propertyFunc, BaseValidator<TProperty> validator);
    }
}
