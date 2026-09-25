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
    /// <see cref="ValidateAsync(T)"/> to run them.
    /// </summary>
    /// <typeparam name="T">The type of the instance being validated.</typeparam>
    public abstract class BaseValidator<T> : IBaseValidator<T>
    {
        protected readonly List<Func<T, Task<ValidationResult>>> _rules = new();
        protected readonly List<Func<bool>> _asyncCheckers = new();

        private bool _descriptiveMessages;
        private bool _stopOnFirstFailure;

        private sealed class CancellableRule
        {
            private readonly Func<T, CancellationToken, Task<ValidationResult>> _rule;

            public CancellableRule(Func<T, CancellationToken, Task<ValidationResult>> rule)
            {
                _rule = rule;
            }

            public Task<ValidationResult> RunAsync(T instance, CancellationToken cancellationToken) => _rule(instance, cancellationToken);

            public Task<ValidationResult> RunAsync(T instance) => _rule(instance, CancellationToken.None);
        }

        private void AddRule(Func<T, CancellationToken, Task<ValidationResult>> rule)
        {
            _rules.Add(new CancellableRule(rule).RunAsync);
        }

        /// <summary>
        /// <see langword="true"/> if any registered rule (including rules of nested, collection or
        /// registry validators) is asynchronous. When <see langword="true"/>, <see cref="Validate"/>
        /// throws and <see cref="ValidateAsync(T)"/> must be used instead.
        /// </summary>
        public bool HasAsyncRules => _asyncCheckers.Any(check => check());

        /// <summary>
        /// Opts every <see cref="RuleFor{TProperty}"/> chain of this validator into descriptive
        /// failures: a failing built-in rule reports a message naming the property and the bound it
        /// enforces (e.g. <c>"Name must be between 3 and 100 characters."</c>) and its own error code
        /// from <see cref="BuiltInRuleCodes"/> (e.g. <c>Length</c>), instead of the default
        /// <c>"Validation failed for property."</c> with <c>DefaultRule</c>.
        /// </summary>
        /// <param name="enabled"><see langword="false"/> switches descriptive failures back off.</param>
        /// <returns>This validator, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Opt-in and additive: without this call every message and error code is exactly what it was
        /// before. Call it anywhere in the derived validator's constructor — the setting is read when
        /// validation runs, so the rules registered before it are covered too.
        /// </para>
        /// <para>
        /// The setting belongs to this validator alone and is not propagated to the validators passed
        /// to <see cref="ValidateNested{TProperty}"/>, <see cref="ValidateCollection{TCollection, TElement}"/>
        /// or <see cref="ValidateRegistryRules{TProperty}"/>, since those are independent instances that
        /// may be shared: call it on each validator whose messages should be descriptive. A single chain
        /// can opt in or out on its own with
        /// <see cref="Builders.ValidationRuleBuilder{T, TProperty}.WithDescriptiveMessages"/>.
        /// </para>
        /// <para>
        /// <see cref="Builders.ValidationRuleBuilder{T, TProperty}.WithMessage"/> always wins, and custom
        /// rules (<c>Must</c>, <c>MustAsync</c>, <c>Should</c>, <c>ShouldAsync</c>) and <c>RequiredIf</c>
        /// are unaffected.
        /// </para>
        /// </remarks>
        public BaseValidator<T> UseDescriptiveMessages(bool enabled = true)
        {
            _descriptiveMessages = enabled;
            return this;
        }

        /// <summary>
        /// Opts every <see cref="RuleFor{TProperty}"/> chain of this validator into stopping at the
        /// first failing rule: once a rule on a chain fails, every rule registered after it on that
        /// same chain is skipped — an async rule after the failure is not even awaited.
        /// </summary>
        /// <param name="enabled"><see langword="false"/> switches the behavior back off.</param>
        /// <returns>This validator, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Opt-in and additive: without this call every chain runs exactly as it did before, all the
        /// way through. Call it anywhere in the derived validator's constructor — the setting is read
        /// when validation runs, so chains registered before it are covered too.
        /// </para>
        /// <para>
        /// A single chain can override this default in either direction with
        /// <see cref="Builders.ValidationRuleBuilder{T, TProperty}.StopOnFirstFailure"/>. The setting
        /// belongs to this validator alone and is not propagated to the validators passed to
        /// <see cref="ValidateNested{TProperty}"/>, <see cref="ValidateCollection{TCollection, TElement}"/>
        /// or <see cref="ValidateRegistryRules{TProperty}"/>.
        /// </para>
        /// </remarks>
        public BaseValidator<T> UseStopOnFirstFailure(bool enabled = true)
        {
            _stopOnFirstFailure = enabled;
            return this;
        }

        /// <summary>
        /// Starts a fluent rule chain for a single property of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property being validated.</typeparam>
        /// <param name="property">An expression selecting the property, e.g. <c>x => x.Name</c>.</param>
        /// <returns>A builder used to attach rules to the selected property.</returns>
        public ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> property)
        {
            var builder = new ValidationRuleBuilder<T, TProperty>(property, () => _descriptiveMessages, () => _stopOnFirstFailure);
            AddRule(async (instance, cancellationToken) => await builder.ValidateAsync(instance, cancellationToken));
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

            AddRule(async (instance, cancellationToken) =>
            {
                var nestedObj = propertyFunc(instance);

                if (nestedObj == null)
                    return new ValidationResult();

                return await builder.ValidateAsync(instance, cancellationToken);
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

            AddRule(async (instance, cancellationToken) => await builder.ValidateAsync(instance, cancellationToken));
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

            AddRule(async (instance, cancellationToken) => await builder.ValidateAsync(instance, cancellationToken));
            _asyncCheckers.Add(() => validator.HasAsyncRules);

            return builder;
        }

        /// <summary>
        /// Runs all registered rules synchronously and returns the aggregated result.
        /// </summary>
        /// <param name="instance">The instance to validate. Must not be <see langword="null"/>.</param>
        /// <returns>The aggregated <see cref="ValidationResult"/> across all registered rules.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="HasAsyncRules"/> is <see langword="true"/>; use <see cref="ValidateAsync(T)"/> instead.
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
        public Task<ValidationResult> ValidateAsync(T instance) => ValidateAsync(instance, CancellationToken.None);

        /// <summary>
        /// Runs all registered rules asynchronously, passing <paramref name="cancellationToken"/> to the
        /// rules that accept one, and returns the aggregated result.
        /// </summary>
        /// <param name="instance">The instance to validate. Must not be <see langword="null"/>.</param>
        /// <param name="cancellationToken">
        /// Token observed between rules and inside every rule registered through a <c>MustAsync</c> or
        /// <c>ShouldAsync</c> overload that takes one. Passing <see cref="CancellationToken.None"/> is
        /// exactly the same as calling <see cref="ValidateAsync(T)"/>.
        /// </param>
        /// <returns>The aggregated <see cref="ValidationResult"/> across all registered rules.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> was cancelled. Cancellation is <b>not</b> turned into a
        /// validation failure: an aborted run has no verdict about the instance, so reporting one would
        /// tell the caller the instance is invalid when it may well be valid. The exception leaves
        /// <c>ValidateAsync</c> and the partial <see cref="ValidationResult"/> is discarded.
        /// </exception>
        public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance), "The instance to validate cannot be null.");

            cancellationToken.ThrowIfCancellationRequested();

            var result = new ValidationResult();

            foreach (var rule in _rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ruleResult = rule.Target is CancellableRule cancellableRule
                    ? await cancellableRule.RunAsync(instance, cancellationToken)
                    : await rule(instance);

                if (!ruleResult.IsValid) result.Merge(ruleResult);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return result;
        }

    }
}
