using FlowValidate.Enums;
using FlowValidate.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace FlowValidate.Builders
{
    /// <summary>
    /// Fluent builder for attaching validation rules to a single property of <typeparamref name="T"/>,
    /// returned by <see cref="BaseValidator{T}.RuleFor{TProperty}"/>.
    /// </summary>
    /// <typeparam name="T">The type owning the property being validated.</typeparam>
    /// <typeparam name="TProperty">The type of the property being validated.</typeparam>
    public class ValidationRuleBuilder<T, TProperty>
    {
        private readonly Expression<Func<T, TProperty>> _property;
        private readonly Func<T, TProperty> _propertyFunc;
        private readonly string _propertyName;
        private readonly List<(Delegate rule, ValidationFailure? validationFailure, bool isFromShould, (string? ErrorMessage, string? ErrorCode)? messageOverride, Severity? severityOverride, (string ErrorMessage, string ErrorCode)? builtInDescription)> _rulesWithMessages = new();
        private readonly List<Func<T, bool>> _chainConditions = new();
        private readonly Func<bool>? _validatorDescriptiveMessages;
        private readonly Func<bool>? _validatorStopOnFirstFailure;
        private bool? _chainDescriptiveMessages;
        private bool? _chainStopOnFirstFailure;

        public ValidationRuleBuilder(Expression<Func<T, TProperty>> property)
        {
            _property = property;
            _propertyFunc = _property.Compile();
            _propertyName = GetPropertyName(property);
        }

        internal ValidationRuleBuilder(Expression<Func<T, TProperty>> property, Func<bool>? validatorDescriptiveMessages)
            : this(property)
        {
            _validatorDescriptiveMessages = validatorDescriptiveMessages;
        }

        internal ValidationRuleBuilder(
            Expression<Func<T, TProperty>> property,
            Func<bool>? validatorDescriptiveMessages,
            Func<bool>? validatorStopOnFirstFailure)
            : this(property, validatorDescriptiveMessages)
        {
            _validatorStopOnFirstFailure = validatorStopOnFirstFailure;
        }

        internal string PropertyName => _propertyName;

        private bool DescriptiveMessagesEnabled =>
            _chainDescriptiveMessages ?? _validatorDescriptiveMessages?.Invoke() ?? false;

        private bool StopOnFirstFailureEnabled =>
            _chainStopOnFirstFailure ?? _validatorStopOnFirstFailure?.Invoke() ?? false;

        internal ValidationRuleBuilder<T, TProperty> AddBuiltInRule(Func<TProperty, bool> rule, string errorCode, Func<string, string> describe)
        {
            _rulesWithMessages.Add((rule, null, false, null, null, (describe(_propertyName), errorCode)));
            return this;
        }

        private static string GetPropertyName(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberExpr)
                return memberExpr.Member.Name;

            return expression.Body.ToString();
        }

        private static bool CompareAgainstNow(TProperty value, bool expectFuture)
        {
            if (value is DateTime dateTimeValue)
            {
                DateTime now = dateTimeValue.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
                return expectFuture ? dateTimeValue > now : dateTimeValue < now;
            }

            if (value is DateTimeOffset dateTimeOffsetValue)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                return expectFuture ? dateTimeOffsetValue > now : dateTimeOffsetValue < now;
            }

#if NET6_0_OR_GREATER
            if (value is DateOnly dateOnlyValue)
            {
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                return expectFuture ? dateOnlyValue > today : dateOnlyValue < today;
            }
#endif

            return false;
        }

        private static bool TryConvertToInt32(TProperty value, out int converted)
        {
            try
            {
                converted = Convert.ToInt32(value);
                return true;
            }
            catch (OverflowException)
            {
                converted = 0;
                return false;
            }
            catch (FormatException)
            {
                converted = 0;
                return false;
            }
            catch (InvalidCastException)
            {
                converted = 0;
                return false;
            }
        }

        /// <summary>
        /// Overrides the error message and error code of the most recently added rule.
        /// Has no effect if no rule has been added yet.
        /// </summary>
        /// <param name="errorMessage">The message to use instead of the rule's default.</param>
        /// <param name="errorCode">The error code to use instead of the rule's default.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> WithMessage(string errorMessage, string? errorCode = null)
        {
            if (_rulesWithMessages.Count == 0)
                return this;

            var lastRule = _rulesWithMessages.Last();

            if (lastRule.validationFailure == null)
            {
                _rulesWithMessages[_rulesWithMessages.Count - 1] =
                    (lastRule.rule, null, lastRule.isFromShould, (errorMessage, errorCode), lastRule.severityOverride, lastRule.builtInDescription);
            }
            else
            {
                var vf = lastRule.validationFailure;
                var updatedMessage = errorMessage ?? vf.ErrorMessage ?? "Validation failed for property.";
                var updatedCode = errorCode ?? vf.ErrorCode ?? "DefaultRule";

                _rulesWithMessages[_rulesWithMessages.Count - 1] =
                    (lastRule.rule, new ValidationFailure(
                        propertyName: vf.PropertyName,
                        errorMessage: updatedMessage,
                        attemptedValue: vf.AttemptedValue,
                        errorCode: updatedCode,
                        severity: vf.Severity
                    ), lastRule.isFromShould, lastRule.messageOverride, lastRule.severityOverride, lastRule.builtInDescription);
            }

            return this;
        }

        /// <summary>
        /// Overrides the severity of the most recently added rule's failure. Has no effect if no rule
        /// has been added yet, following the same contract as <see cref="WithMessage"/>.
        /// </summary>
        /// <param name="severity">The severity to record on the failure instead of <see cref="Severity.Error"/>.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// Can be combined with <see cref="WithMessage"/> in either order on the same rule. Like
        /// <see cref="WithMessage"/>, it has no effect on failures raised from inside a
        /// <c>Should</c>/<c>ShouldAsync</c> callback, since those build their own
        /// <see cref="ValidationFailure"/> instances directly and never read this chain's overrides.
        /// The default <see cref="Severity.Error"/> is unchanged when this method is not called, and
        /// <see cref="ValidationResult.IsValid"/> still turns <see langword="false"/> for a failing rule
        /// regardless of the severity recorded on it.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> WithSeverity(Severity severity)
        {
            if (_rulesWithMessages.Count == 0)
                return this;

            var lastRule = _rulesWithMessages.Last();

            _rulesWithMessages[_rulesWithMessages.Count - 1] =
                (lastRule.rule, lastRule.validationFailure, lastRule.isFromShould, lastRule.messageOverride, severity, lastRule.builtInDescription);

            return this;
        }

        /// <summary>
        /// Opts this rule chain into descriptive failures: every built-in rule on it reports a message
        /// naming the property and the bound it enforces (e.g. <c>"Name must be between 3 and 100 characters."</c>)
        /// and its own error code (e.g. <c>Length</c>) instead of the default
        /// <c>"Validation failed for property."</c> with <c>DefaultRule</c>.
        /// </summary>
        /// <param name="enabled">
        /// <see langword="false"/> turns descriptive failures back off for this chain, overriding
        /// <see cref="BaseValidator{T}.UseDescriptiveMessages"/> on the validator that created it.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Opt-in: without this call, and without <see cref="BaseValidator{T}.UseDescriptiveMessages"/>
        /// on the owning validator, messages and codes are exactly what they were before. The setting
        /// applies to the whole chain no matter where it is written, and is read when validation runs,
        /// so it also covers rules added before it.
        /// </para>
        /// <para>
        /// <see cref="WithMessage"/> always wins: its message replaces the descriptive one, and its
        /// error code replaces the rule's code when one is passed. Custom rules
        /// (<c>Must</c>, <c>MustAsync</c>, <c>Should</c>, <c>ShouldAsync</c>) and <see cref="RequiredIf"/>
        /// are unaffected — they keep the default message and code, and <c>Required</c>, respectively.
        /// </para>
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> WithDescriptiveMessages(bool enabled = true)
        {
            _chainDescriptiveMessages = enabled;
            return this;
        }

        /// <summary>
        /// Opts this rule chain into stopping at the first failing rule: once a rule on this chain
        /// fails, every rule registered after it is skipped — an async rule after the failure is not
        /// even awaited.
        /// </summary>
        /// <param name="enabled">
        /// <see langword="false"/> turns the behavior back off for this chain, overriding
        /// <see cref="BaseValidator{T}.UseStopOnFirstFailure"/> on the validator that created it.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Opt-in and additive: without this call, and without
        /// <see cref="BaseValidator{T}.UseStopOnFirstFailure"/> on the owning validator, every rule on
        /// the chain still runs, exactly as before. The setting applies to the <b>whole chain</b> no
        /// matter where it is written, just like <see cref="WithDescriptiveMessages"/>, and is read when
        /// validation runs, so it also covers rules added before it.
        /// </para>
        /// <para>
        /// A <c>Should</c>/<c>ShouldAsync</c> rule can report more than one failure from a single call;
        /// when it fails, every failure it raised is still recorded before the chain stops.
        /// </para>
        /// <para>
        /// <see cref="RequiredIf"/> already stops the remaining rules on its own via
        /// <see cref="ValidationResult.SkipRemainingRules"/> and keeps doing so regardless of this
        /// setting.
        /// </para>
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> StopOnFirstFailure(bool enabled = true)
        {
            _chainStopOnFirstFailure = enabled;
            return this;
        }


        /// <summary>
        /// Runs every rule registered on this property, in registration order, stopping early if
        /// a rule sets <see cref="ValidationResult.SkipRemainingRules"/>. Returns an empty, valid
        /// result without reading the property when a condition added by <see cref="When"/> or
        /// <see cref="Unless"/> is not met.
        /// </summary>
        /// <param name="instance">The parent instance the property belongs to.</param>
        /// <returns>The aggregated <see cref="ValidationResult"/> for this property.</returns>
        public Task<ValidationResult> ValidateAsync(T instance) => ValidateAsync(instance, CancellationToken.None);

        /// <summary>
        /// Runs every rule registered on this property, in registration order, passing
        /// <paramref name="cancellationToken"/> to the rules that accept one and observing it between
        /// rules. Behaves exactly like <see cref="ValidateAsync(T)"/> when the token is
        /// <see cref="CancellationToken.None"/>.
        /// </summary>
        /// <param name="instance">The parent instance the property belongs to.</param>
        /// <param name="cancellationToken">Token observed while the rules run.</param>
        /// <returns>The aggregated <see cref="ValidationResult"/> for this property.</returns>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> was cancelled. Cancellation is not reported as a rule
        /// failure — an aborted run has no verdict about the value.
        /// </exception>
        public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken)
        {
            var result = new ValidationResult();

            cancellationToken.ThrowIfCancellationRequested();

            if (_chainConditions.Any(condition => !condition(instance)))
                return result;

            var value = _propertyFunc(instance);

            var describeBuiltInRules = DescriptiveMessagesEnabled;
            var stopOnFirstFailure = StopOnFirstFailureEnabled;

            foreach (var (rule, validationFailure, isFromShould, messageOverride, severityOverride, builtInDescription) in _rulesWithMessages)
            {
                if (result.SkipRemainingRules) break;

                cancellationToken.ThrowIfCancellationRequested();

                var failuresBeforeRule = result.Failures.Count;

                bool passed = true;

                if (rule is Func<TProperty, bool> r1)
                {
                    passed = r1(value);
                }
                else if (rule is Func<T, TProperty, ValidationResult, bool> r2)
                {
                    passed = r2(instance, value, result);
                }
                else if (rule is Func<TProperty, Task<bool>> r3)
                {
                    passed = await r3(value);
                }
                else if (rule is Func<T, TProperty, ValidationResult, Task<bool>> r4)
                {
                    passed = await r4(instance, value, result);
                }
                else if (rule is Func<TProperty, CancellationToken, Task<bool>> r5)
                {
                    passed = await r5(value, cancellationToken);
                }
                else if (rule is Func<T, TProperty, ValidationResult, CancellationToken, Task<bool>> r6)
                {
                    passed = await r6(instance, value, result, cancellationToken);
                }

                if (!passed)
                {
                    if (!isFromShould)
                    {
                        var description = describeBuiltInRules ? builtInDescription : null;

                        var baseFailure = validationFailure ?? new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: messageOverride?.ErrorMessage ?? description?.ErrorMessage ?? "Validation failed for property.",
                            attemptedValue: value,
                            errorCode: messageOverride?.ErrorCode ?? description?.ErrorCode ?? "DefaultRule"
                        );

                        var failure = severityOverride.HasValue
                            ? new ValidationFailure(
                                propertyName: baseFailure.PropertyName,
                                errorMessage: baseFailure.ErrorMessage,
                                attemptedValue: baseFailure.AttemptedValue,
                                errorCode: baseFailure.ErrorCode,
                                severity: severityOverride.Value
                            )
                            : baseFailure;

                        result.AddFailure(failure);
                    }

                    result.SetIsValid(false);
                }

                if (stopOnFirstFailure && result.Failures.Count > failuresBeforeRule)
                    break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            return result;
        }

        /// <summary>
        /// Runs this property's whole rule chain only when <paramref name="condition"/> holds for the
        /// root instance. When it does not hold, every rule on this chain is skipped and
        /// <b>no failure is produced</b> — the property is not even read.
        /// </summary>
        /// <param name="condition">
        /// Evaluated against the root instance being validated, so it can look at other properties,
        /// e.g. <c>x =&gt; x.Country == "TR"</c>.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <para>
        /// The condition guards the <b>entire</b> chain, not just the rule before it: placement inside
        /// the chain does not matter, so <c>RuleFor(x =&gt; x.Vat).IsNotEmpty().When(x =&gt; x.IsCompany)</c>
        /// and <c>RuleFor(x =&gt; x.Vat).When(x =&gt; x.IsCompany).IsNotEmpty()</c> behave identically.
        /// Start a second <c>RuleFor</c> chain on the same property to give it a different condition.
        /// </para>
        /// <para>
        /// Several <see cref="When"/> / <see cref="Unless"/> calls on the same chain are combined with
        /// AND — the chain runs only if all of them are met. Asynchronous rules
        /// (<c>MustAsync</c>, <c>ShouldAsync</c>) are gated the same way, but a skipped chain
        /// still counts towards <see cref="HasAsyncRules"/>, so <see cref="BaseValidator{T}.Validate"/>
        /// keeps throwing for a validator that declares async rules.
        /// </para>
        /// <para>
        /// Unlike <see cref="RequiredIf"/>, which fails with <c>"Property is required."</c> when its
        /// condition on the property's own value is not met, an unmet <see cref="When"/> condition is
        /// silent. <see cref="RequiredIf"/> is unchanged and can still be used inside a conditional chain.
        /// </para>
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> When(Func<T, bool> condition)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            _chainConditions.Add(condition);

            return this;
        }

        /// <summary>
        /// The inverse of <see cref="When"/>: runs this property's whole rule chain only when
        /// <paramref name="condition"/> does <b>not</b> hold for the root instance. When it holds,
        /// every rule on this chain is skipped and no failure is produced.
        /// </summary>
        /// <param name="condition">
        /// Evaluated against the root instance being validated, so it can look at other properties,
        /// e.g. <c>x =&gt; x.IsDraft</c>.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Scoping, combination and async behaviour are identical to <see cref="When"/>:
        /// <c>Unless(c)</c> is exactly <c>When(x =&gt; !c(x))</c>.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> Unless(Func<T, bool> condition)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            _chainConditions.Add(instance => !condition(instance));

            return this;
        }

        /// <summary>
        /// Makes the property required when <paramref name="condition"/> evaluates to <see langword="false"/>
        /// for its current value: the rule fails with <c>"Property is required."</c> and any remaining
        /// rules for this property are skipped. When <paramref name="condition"/> is <see langword="true"/>,
        /// the property is treated as required (non-null, non-blank for strings).
        /// </summary>
        /// <param name="condition">Evaluated against the property's own value.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// To skip rules silently instead of failing, or to branch on another property of the root
        /// instance, use <see cref="When"/> or <see cref="Unless"/>.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> RequiredIf(Func<TProperty, bool> condition)
        {
            _rulesWithMessages.Add((
                  (Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
                  {
                      if (!condition(value))
                      {
                          result.SetSkipRemainingRules(true);
                          return false;
                      }

                      if (value is string str)
                          return !string.IsNullOrWhiteSpace(str);

                      return value != null;
                  }),
                      new ValidationFailure(
                          propertyName: _propertyName,
                          errorMessage: "Property is required.",
                          attemptedValue: null,
                          errorCode: "Required"
                      ),
                  false,
                  null,
                  null,
                  null
              ));

            return this;
        }

        /// <summary>
        /// Adds a custom synchronous rule: the property fails when <paramref name="rule"/> returns
        /// <see langword="false"/>, using the default or overridden failure message.
        /// </summary>
        /// <param name="rule">Predicate evaluated against the property's value.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Must(Func<TProperty, bool> rule)
        {
            _rulesWithMessages.Add((rule, null, false, null, null, null));
            return this;
        }

        /// <summary>
        /// Adds a custom asynchronous rule: the property fails when <paramref name="rule"/> resolves to
        /// <see langword="false"/>. Makes this validator's <see cref="HasAsyncRules"/> return <see langword="true"/>.
        /// </summary>
        /// <param name="rule">Async predicate evaluated against the property's value.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> MustAsync(Func<TProperty, Task<bool>> rule)
        {
            _rulesWithMessages.Add((rule, null, false, null, null, null));
            return this;
        }

        /// <summary>
        /// Adds a custom asynchronous rule that receives the <see cref="CancellationToken"/> passed to
        /// <see cref="BaseValidator{T}.ValidateAsync(T, CancellationToken)"/>, so the work it starts
        /// (a database or HTTP call) can be aborted with the request. Makes this validator's
        /// <see cref="HasAsyncRules"/> return <see langword="true"/>.
        /// </summary>
        /// <param name="rule">Async predicate evaluated against the property's value and the token.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// The token is <see cref="CancellationToken.None"/> when validation is started through
        /// <see cref="BaseValidator{T}.ValidateAsync(T)"/> or <see cref="BaseValidator{T}.Validate"/>.
        /// An <see cref="OperationCanceledException"/> thrown by <paramref name="rule"/> after the token
        /// is cancelled is not turned into a validation failure; it leaves <c>ValidateAsync</c>.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, Task<bool>> rule)
        {
            _rulesWithMessages.Add((rule, null, false, null, null, null));
            return this;
        }

        /// <summary>
        /// Fails when the value is <see langword="null"/>, or is a blank/whitespace-only string.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsNotEmpty()
        {
            return AddBuiltInRule(value =>
            {
                if (value is string str)
                    return !string.IsNullOrWhiteSpace(str);

                return value != null;
            },
            BuiltInRuleCodes.NotEmpty,
            name => $"{name} must not be empty.");
        }

        /// <summary>
        /// Fails unless the value equals <paramref name="expectedValue"/> using the default equality comparer.
        /// </summary>
        /// <param name="expectedValue">The value the property must equal.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsEqual(TProperty expectedValue)
        {
            return AddBuiltInRule(
                value => EqualityComparer<TProperty>.Default.Equals(value, expectedValue),
                BuiltInRuleCodes.Equal,
                name => $"{name} must be equal to '{expectedValue}'.");
        }

        /// <summary>
        /// Fails unless the value's string representation contains <paramref name="substring"/>.
        /// </summary>
        /// <param name="substring">The substring the value must contain.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Contains(string substring)
        {
            return AddBuiltInRule(
                value => value != null && value.ToString() is string str && str.Contains(substring),
                BuiltInRuleCodes.Contains,
                name => $"{name} must contain '{substring}'.");
        }

        /// <summary>
        /// Fails unless the value is an <see cref="int"/> within <c>[minValue, maxValue]</c> (inclusive).
        /// Non-<see cref="int"/> values always fail. For other numeric types, see the
        /// <see cref="IComparable{T}"/>-based overloads.
        /// </summary>
        /// <param name="minValue">The inclusive lower bound.</param>
        /// <param name="maxValue">The inclusive upper bound.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsInRange(int minValue, int maxValue)
        {
            return AddBuiltInRule(value =>
            {
                if (value is int intValue)
                {
                    return intValue >= minValue && intValue <= maxValue;
                }
                return false;
            },
            BuiltInRuleCodes.InRange,
            name => $"{name} must be between {minValue} and {maxValue}.");
        }

        /// <summary>
        /// Fails unless the value is a string matching a basic <c>local@domain.tld</c> email pattern.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsEmail()
        {
            return AddBuiltInRule(value =>
            {
                if (value is string email)
                {
                    return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                }
                return false;
            },
            BuiltInRuleCodes.Email,
            name => $"{name} must be a valid email address.");
        }

        /// <summary>
        /// Fails unless the value's string representation length is within
        /// <c>[minLength, maxLength]</c> (inclusive).
        /// </summary>
        /// <param name="minLength">The inclusive minimum length.</param>
        /// <param name="maxLength">The inclusive maximum length.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Length(int minLength, int maxLength)
        {
            return AddBuiltInRule(
                value => value != null && value.ToString() is string str && str.Length >= minLength && str.Length <= maxLength,
                BuiltInRuleCodes.Length,
                name => $"{name} must be between {minLength} and {maxLength} characters.");
        }

        /// <summary>
        /// Fails unless the value converts to <see cref="int"/> and is strictly greater than <paramref name="minValue"/>.
        /// A value that cannot be converted to <see cref="int"/> fails as well.
        /// </summary>
        /// <param name="minValue">The exclusive lower bound.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsGreaterThan(int minValue)
        {
            return AddBuiltInRule(
                value => TryConvertToInt32(value, out var intValue) && intValue > minValue,
                BuiltInRuleCodes.GreaterThan,
                name => $"{name} must be greater than {minValue}.");
        }

        /// <summary>
        /// Fails unless the value is a string matching the given regular expression.
        /// </summary>
        /// <param name="pattern">The regular expression the value must match.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// This overload matches without a time limit. For values that come from untrusted input —
        /// a request body validated by the middleware, for example — prefer
        /// <see cref="MatchesRegex(string, TimeSpan)"/> or <see cref="MatchesRegex(Regex)"/>, so a
        /// pattern that backtracks catastrophically cannot occupy the thread indefinitely.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> MatchesRegex(string pattern)
        {
            return AddBuiltInRule(value =>
            {
                if (value is string stringValue)
                {
                    return Regex.IsMatch(stringValue, pattern);
                }
                return false;
            },
            BuiltInRuleCodes.RegexMatch,
            name => $"{name} must match the required pattern.");
        }

        /// <summary>
        /// Fails unless the value is a string matching the given regular expression, giving up after
        /// <paramref name="matchTimeout"/>.
        /// </summary>
        /// <param name="pattern">The regular expression the value must match.</param>
        /// <param name="matchTimeout">
        /// How long a single match attempt may run before it is abandoned. Use this on untrusted input:
        /// it bounds the work a catastrophically backtracking pattern can do per value.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException"><paramref name="pattern"/> is not a valid regular expression.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="matchTimeout"/> is negative, zero, or greater than approximately 24 days.
        /// </exception>
        /// <remarks>
        /// The pattern is compiled once, when the rule is added, so an invalid pattern throws here
        /// rather than during validation. A value that is <see langword="null"/> or not a string fails
        /// exactly as it does with <see cref="MatchesRegex(string)"/>. A timed-out match does not throw
        /// out of validation: see <see cref="MatchesRegex(Regex)"/> for how it is reported.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> MatchesRegex(string pattern, TimeSpan matchTimeout)
        {
            return MatchesRegex(new Regex(pattern, RegexOptions.None, matchTimeout));
        }

        /// <summary>
        /// Fails unless the value is a string matching <paramref name="regex"/>, which carries its own
        /// options and match timeout.
        /// </summary>
        /// <param name="regex">
        /// A pre-built regular expression. Construct it with a
        /// <see cref="Regex(string, RegexOptions, TimeSpan)"/> timeout when the value being validated
        /// comes from untrusted input; a shared <see langword="static readonly"/> instance also avoids
        /// re-parsing the pattern.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="regex"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A value that is <see langword="null"/> or not a string fails exactly as it does with
        /// <see cref="MatchesRegex(string)"/>. When the match exceeds the regex's own timeout, the
        /// <see cref="RegexMatchTimeoutException"/> is not allowed to escape: the property is reported
        /// as invalid with the error code <c>RegexTimeout</c>, which distinguishes an abandoned match
        /// from a value that genuinely did not match. That timeout failure keeps its own message even
        /// when <see cref="WithMessage"/> is chained after the rule, since the two outcomes mean
        /// different things; <see cref="WithMessage"/> still overrides the ordinary no-match failure.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> MatchesRegex(Regex regex)
        {
            if (regex == null)
                throw new ArgumentNullException(nameof(regex));

            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
                {
                    if (value is not string stringValue)
                        return false;

                    try
                    {
                        return regex.IsMatch(stringValue);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: $"Regular expression match timed out after {regex.MatchTimeout}.",
                            attemptedValue: value,
                            errorCode: "RegexTimeout"
                        ));

                        return true;
                    }
                }),
                null,
                false,
                null,
                null,
                ($"{_propertyName} must match the required pattern.", BuiltInRuleCodes.RegexMatch)
            ));

            return this;
        }

        /// <summary>
        /// Fails unless the value is strictly later than the current moment. Supports
        /// <see cref="DateTime"/> (compared against <see cref="DateTime.UtcNow"/> when
        /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>, otherwise against
        /// <see cref="DateTime.Now"/>), <see cref="DateTimeOffset"/> (compared against
        /// <see cref="DateTimeOffset.UtcNow"/>), and, on target frameworks that support it,
        /// <see cref="DateOnly"/> (compared against today's local date). A <see langword="null"/>
        /// value or any other type always fails.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsDateInFuture()
        {
            return AddBuiltInRule(
                value => CompareAgainstNow(value, expectFuture: true),
                BuiltInRuleCodes.DateInFuture,
                name => $"{name} must be a date in the future.");
        }

        /// <summary>
        /// Fails unless the value is a non-string <see cref="System.Collections.IEnumerable"/> whose
        /// items are all distinct. Non-collection values always fail.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsUnique()
        {
            return AddBuiltInRule(value =>
            {
                if (value is not string && value is System.Collections.IEnumerable collection)
                {
                    var items = collection.Cast<object>().ToList();
                    return items.Distinct().Count() == items.Count;
                }
                return false;
            },
            BuiltInRuleCodes.Unique,
            name => $"{name} must contain unique items.");
        }

        /// <summary>
        /// Fails unless the value converts to <see cref="int"/> and is strictly less than <paramref name="maxValue"/>.
        /// A value that cannot be converted to <see cref="int"/> fails as well.
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsLessThan(int maxValue)
        {
            return AddBuiltInRule(
                value => TryConvertToInt32(value, out var intValue) && intValue < maxValue,
                BuiltInRuleCodes.LessThan,
                name => $"{name} must be less than {maxValue}.");
        }

        /// <summary>
        /// Fails unless the value is strictly earlier than the current moment. Supports
        /// <see cref="DateTime"/> (compared against <see cref="DateTime.UtcNow"/> when
        /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>, otherwise against
        /// <see cref="DateTime.Now"/>), <see cref="DateTimeOffset"/> (compared against
        /// <see cref="DateTimeOffset.UtcNow"/>), and, on target frameworks that support it,
        /// <see cref="DateOnly"/> (compared against today's local date). A <see langword="null"/>
        /// value or any other type always fails.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsDateInPast()
        {
            return AddBuiltInRule(
                value => CompareAgainstNow(value, expectFuture: false),
                BuiltInRuleCodes.DateInPast,
                name => $"{name} must be a date in the past.");
        }

        /// <summary>
        /// Fails unless the value is strictly later than the current moment.
        /// Equivalent to <see cref="IsDateInFuture"/>; see it for the per-type comparison rules.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsInFuture()
        {
            return AddBuiltInRule(
                value => CompareAgainstNow(value, expectFuture: true),
                BuiltInRuleCodes.InFuture,
                name => $"{name} must be a date in the future.");
        }

        /// <summary>
        /// Fails when the value's string representation contains the same character repeated
        /// <paramref name="maxRepeatLength"/> or more times in a row. A <see langword="null"/> value
        /// or one that stringifies to <see langword="null"/> passes.
        /// </summary>
        /// <param name="maxRepeatLength">The repeat run length that triggers failure. Defaults to 3.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> NoConsecutiveRepeats(int maxRepeatLength = 3)
        {
            return AddBuiltInRule(value =>
            {
                if (value == null) return true;

                string? str = value.ToString();
                if (str == null) return true;
                int count = 1;

                for (int i = 1; i < str.Length; i++)
                {
                    if (str[i] == str[i - 1])
                    {
                        count++;
                        if (count >= maxRepeatLength)
                            return false;
                    }
                    else
                    {
                        count = 1;
                    }
                }

                return true;
            },
            BuiltInRuleCodes.NoConsecutiveRepeats,
            name => $"{name} must not repeat the same character {maxRepeatLength} or more times in a row.");
        }

        /// <summary>
        /// Fails unless the value's string representation reads the same forwards and backwards
        /// (case-insensitive). A <see langword="null"/> value or one that stringifies to <see langword="null"/> passes.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsPalindrome()
        {
            return AddBuiltInRule(value =>
            {
                if (value == null)
                    return true;

                string? str = value.ToString()?.ToLowerInvariant();
                if (str == null) return true;

                int length = str.Length;
                for (int i = 0; i < length / 2; i++)
                {
                    if (str[i] != str[length - 1 - i])
                        return false;
                }

                return true;
            },
            BuiltInRuleCodes.Palindrome,
            name => $"{name} must be a palindrome.");
        }

        /// <summary>
        /// Adds a custom rule that can raise zero, one, or several errors via the callback passed to
        /// <paramref name="action"/>, instead of returning a single pass/fail. An unhandled exception
        /// inside <paramref name="action"/> is recorded as a failure.
        /// </summary>
        /// <param name="action">
        /// Receives the property's value and an <c>error</c> callback that records one failure per call.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty, Action<string>> action)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
                {
                    try
                    {
                        action(value, error =>
                        {
                            result.AddFailure(new ValidationFailure(
                                propertyName: _propertyName,
                                errorMessage: error,
                                attemptedValue: value,
                                errorCode: "ShouldRule"
                            ));
                        });
                        return true;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: "Unexpected exception in Should rule.",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="Should(Action{TProperty, Action{string}})"/>: can raise
        /// zero, one, or several errors via the callback passed to <paramref name="action"/>. Makes this
        /// validator's <see cref="HasAsyncRules"/> return <see langword="true"/>.
        /// </summary>
        /// <param name="action">
        /// Receives the property's value and an <c>error</c> callback that records one failure per call.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> ShouldAsync(Func<TProperty, Action<string>, Task> action)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, Task<bool>>)(async (instance, value, result) =>
                {
                    try
                    {
                        await action(value, error =>
                        {
                            result.AddFailure(new ValidationFailure(
                                propertyName: _propertyName,
                                errorMessage: error,
                                attemptedValue: value,
                                errorCode: "ShouldRule"
                            ));
                        });
                        return true;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: "Unexpected exception in Should rule.",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="Should(Action{TProperty, Action{string}})"/> that also
        /// receives the <see cref="CancellationToken"/> passed to
        /// <see cref="BaseValidator{T}.ValidateAsync(T, CancellationToken)"/>. Makes this validator's
        /// <see cref="HasAsyncRules"/> return <see langword="true"/>.
        /// </summary>
        /// <param name="action">
        /// Receives the property's value, an <c>error</c> callback that records one failure per call, and
        /// the token.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// An exception thrown by <paramref name="action"/> is still recorded as a single failure, with one
        /// exception: once the token is cancelled, an <see cref="OperationCanceledException"/> is rethrown
        /// instead of being reported, because an aborted run has no verdict about the value. Any other
        /// exception, and an <see cref="OperationCanceledException"/> raised by unrelated work (a token the
        /// rule owns itself), keeps the existing behavior.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> ShouldAsync(Func<TProperty, Action<string>, CancellationToken, Task> action)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, CancellationToken, Task<bool>>)(async (instance, value, result, cancellationToken) =>
                {
                    try
                    {
                        await action(value, error =>
                        {
                            result.AddFailure(new ValidationFailure(
                                propertyName: _propertyName,
                                errorMessage: error,
                                attemptedValue: value,
                                errorCode: "ShouldRule"
                            ));
                        }, cancellationToken);
                        return true;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: "Unexpected exception in Should rule.",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }

        /// <summary>
        /// Always returns an empty string; this method never accumulated any errors.
        /// </summary>
        /// <returns>An empty string.</returns>
        [Obsolete("This method never accumulated any errors and always returns an empty string. Use ValidationResult.Failures instead.")]
        public string GetAllErrors() => string.Empty;

        /// <summary>
        /// Adds a custom rule that runs <paramref name="action"/> and records a single failure if it throws.
        /// </summary>
        /// <param name="action">Invoked with the property's value; an exception is treated as failure.</param>
        /// <param name="errorMessage">The failure message used when <paramref name="action"/> throws. Defaults to <c>"Custom validation failed !"</c>.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty> action, string? errorMessage = null)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
                {
                    try
                    {
                        action(value);
                        return true;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: errorMessage ?? "Custom validation failed !",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="Should(Action{TProperty}, string)"/>: runs
        /// <paramref name="action"/> and records a single failure if it throws. Makes this validator's
        /// <see cref="HasAsyncRules"/> return <see langword="true"/>.
        /// </summary>
        /// <param name="action">Invoked with the property's value; an exception is treated as failure.</param>
        /// <param name="errorMessage">The failure message used when <paramref name="action"/> throws. Defaults to <c>"Custom validation failed !"</c>.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> ShouldAsync(Func<TProperty, Task> action, string? errorMessage = null)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, Task<bool>>)(async (instance, value, result) =>
                {
                    try
                    {
                        await action(value);
                        return true;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: errorMessage ?? "Custom validation failed !",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="Should(Action{TProperty}, string)"/> that also receives
        /// the <see cref="CancellationToken"/> passed to
        /// <see cref="BaseValidator{T}.ValidateAsync(T, CancellationToken)"/>: runs
        /// <paramref name="action"/> and records a single failure if it throws. Makes this validator's
        /// <see cref="HasAsyncRules"/> return <see langword="true"/>.
        /// </summary>
        /// <param name="action">Invoked with the property's value and the token; an exception is treated as failure.</param>
        /// <param name="errorMessage">
        /// The failure message used when <paramref name="action"/> throws; pass <see langword="null"/> for
        /// the default <c>"Custom validation failed !"</c>. It is required rather than optional so that an
        /// existing single-argument <c>ShouldAsync(async (value, addError) =&gt; ...)</c> call keeps
        /// resolving to <see cref="ShouldAsync(Func{TProperty, Action{string}, Task})"/> instead of
        /// becoming ambiguous.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        /// <remarks>
        /// Once the token is cancelled, an <see cref="OperationCanceledException"/> from
        /// <paramref name="action"/> is rethrown instead of being recorded as a failure, because an aborted
        /// run has no verdict about the value. Every other exception keeps the existing behavior.
        /// </remarks>
        public ValidationRuleBuilder<T, TProperty> ShouldAsync(Func<TProperty, CancellationToken, Task> action, string? errorMessage)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, CancellationToken, Task<bool>>)(async (instance, value, result, cancellationToken) =>
                {
                    try
                    {
                        await action(value, cancellationToken);
                        return true;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: errorMessage ?? "Custom validation failed !",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }


        /// <summary>
        /// Overload of <see cref="Should(Action{TProperty}, string)"/> that joins multiple failure
        /// messages with " &amp; " when <paramref name="action"/> throws.
        /// </summary>
        /// <param name="action">Invoked with the property's value; an exception is treated as failure.</param>
        /// <param name="errorMessages">The messages joined into the failure text. Defaults to <c>"Custom validation failed !"</c> when empty.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty> action, params string[] errorMessages)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
                {
                    try
                    {
                        action(value);

                        return true;
                    }
                    catch
                    {
                        result.AddFailure(new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: errorMessages != null && errorMessages.Length > 0
                                ? string.Join(" & ", errorMessages)
                                : "Custom validation failed !",
                            attemptedValue: value,
                            errorCode: "ShouldRuleException"
                        ));
                        return false;
                    }
                }),
                null,
                true,
                null,
                null,
                null
            ));

            return this;
        }

        /// <summary>
        /// <see langword="true"/> if any rule added via a <c>MustAsync</c> or <c>ShouldAsync</c>
        /// overload has been registered on this property.
        /// </summary>
        public bool HasAsyncRules => _rulesWithMessages.Any(r =>
            r.rule is Func<TProperty, Task<bool>> ||
            r.rule is Func<T, TProperty, ValidationResult, Task<bool>> ||
            r.rule is Func<TProperty, CancellationToken, Task<bool>> ||
            r.rule is Func<T, TProperty, ValidationResult, CancellationToken, Task<bool>>);
    }
}
