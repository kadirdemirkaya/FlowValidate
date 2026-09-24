using FlowValidate;
using FlowValidate.Builders;
using FlowValidate.Rules;
using System.Linq.Expressions;

namespace FlowValidate.Abstractions
{
    public interface IBaseValidator<T>
    {
        ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> property);

        Task<ValidationResult> ValidateAsync(T instance);

        /// <summary>
        /// Runs all registered rules asynchronously, passing <paramref name="cancellationToken"/> to the
        /// rules that accept one, and returns the aggregated result.
        /// </summary>
        /// <param name="instance">The instance to validate. Must not be <see langword="null"/>.</param>
        /// <param name="cancellationToken">
        /// Token observed while the rules run. Passing <see cref="CancellationToken.None"/> is exactly the
        /// same as calling <see cref="ValidateAsync(T)"/>.
        /// </param>
        /// <returns>The aggregated <see cref="ValidationResult"/> across all registered rules.</returns>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> was cancelled. Cancellation is <b>not</b> turned into a
        /// validation failure: an aborted run has no verdict about the instance, so the exception is
        /// allowed to leave <c>ValidateAsync</c> and the partial <see cref="ValidationResult"/> is discarded.
        /// </exception>
        /// <remarks>
        /// This member has a default implementation so that adding it does not break types that implement
        /// <see cref="IBaseValidator{T}"/> directly. The default observes the token once, before delegating
        /// to <see cref="ValidateAsync(T)"/>; implementations that run their own asynchronous work should
        /// override it to observe the token throughout. <see cref="BaseValidator{T}"/> does.
        /// </remarks>
        async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await ValidateAsync(instance);
        }

        ValidationResult Validate(T instance);

        ValidationNestedBuilder<T, TProperty> ValidateNested<TProperty>(Func<T, TProperty?> propertyFunc, BaseValidator<TProperty> validator);

        ValidationCollectionBuilder<T, TCollection, TElement> ValidateCollection<TCollection, TElement>(
              Func<T, IEnumerable<TCollection>> collectionFunc,
              BaseValidator<TElement> elementValidator,
              Func<TCollection, TElement> itemSelector);

        ValidationRegistryRules<T, TProperty> ValidateRegistryRules<TProperty>(Func<T, TProperty> propertyFunc, BaseValidator<TProperty> validator);
    }
}
