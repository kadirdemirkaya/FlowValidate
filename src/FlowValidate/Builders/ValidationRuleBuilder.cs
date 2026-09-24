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
        private readonly List<(Delegate rule, ValidationFailure? validationFailure, bool isFromShould, (string ErrorMessage, string? ErrorCode)? messageOverride)> _rulesWithMessages = new();
        private readonly List<Func<T, bool>> _chainConditions = new();

        public ValidationRuleBuilder(Expression<Func<T, TProperty>> property)
        {
            _property = property;
            _propertyFunc = _property.Compile();
            _propertyName = GetPropertyName(property);
        }

        private static string GetPropertyName(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberExpr)
                return memberExpr.Member.Name;

            return expression.Body.ToString();
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
                var defaultMessage = errorMessage ?? "Validation failed for property.";
                var defaultCode = errorCode ?? "DefaultRule";

                _rulesWithMessages[_rulesWithMessages.Count - 1] =
                    (lastRule.rule, null, lastRule.isFromShould, (defaultMessage, defaultCode));
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
                    ), lastRule.isFromShould, lastRule.messageOverride);
            }

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
        public async Task<ValidationResult> ValidateAsync(T instance)
        {
            var result = new ValidationResult();

            if (_chainConditions.Any(condition => !condition(instance)))
                return result;

            var value = _propertyFunc(instance);

            foreach (var (rule, validationFailure, isFromShould, messageOverride) in _rulesWithMessages)
            {
                if (result.SkipRemainingRules) break;

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

                if (!passed)
                {
                    if (!isFromShould)
                    {
                        var failure = validationFailure ?? new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: messageOverride?.ErrorMessage ?? "Validation failed for property.",
                            attemptedValue: value,
                            errorCode: messageOverride?.ErrorCode ?? "DefaultRule"
                        );

                        result.AddFailure(failure);
                    }

                    result.SetIsValid(false);
                }
            }

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
        /// (<see cref="MustAsync"/>, <c>ShouldAsync</c>) are gated the same way, but a skipped chain
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
            _rulesWithMessages.Add((rule, null, false, null));
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
            _rulesWithMessages.Add((rule, null, false, null));
            return this;
        }

        /// <summary>
        /// Fails when the value is <see langword="null"/>, or is a blank/whitespace-only string.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsNotEmpty()
        {
            return Must(value =>
            {
                if (value is string str)
                    return !string.IsNullOrWhiteSpace(str);

                return value != null;
            });
        }

        /// <summary>
        /// Fails unless the value equals <paramref name="expectedValue"/> using the default equality comparer.
        /// </summary>
        /// <param name="expectedValue">The value the property must equal.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsEqual(TProperty expectedValue)
        {
            return Must(value => EqualityComparer<TProperty>.Default.Equals(value, expectedValue));
        }

        /// <summary>
        /// Fails unless the value's string representation contains <paramref name="substring"/>.
        /// </summary>
        /// <param name="substring">The substring the value must contain.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> Contains(string substring)
        {
            return Must(value => value != null && value.ToString() is string str && str.Contains(substring));
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
            return Must(value =>
            {
                if (value is int intValue)
                {
                    return intValue >= minValue && intValue <= maxValue;
                }
                return false;
            });
        }

        /// <summary>
        /// Fails unless the value is a string matching a basic <c>local@domain.tld</c> email pattern.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsEmail()
        {
            return Must(value =>
            {
                if (value is string email)
                {
                    return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                }
                return false;
            });
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
            return Must(value => value != null && value.ToString() is string str && str.Length >= minLength && str.Length <= maxLength);
        }

        /// <summary>
        /// Fails unless the value converts to <see cref="int"/> and is strictly greater than <paramref name="minValue"/>.
        /// A value that cannot be converted to <see cref="int"/> fails as well.
        /// </summary>
        /// <param name="minValue">The exclusive lower bound.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsGreaterThan(int minValue)
        {
            return Must(value => TryConvertToInt32(value, out var intValue) && intValue > minValue);
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
            return Must(value =>
            {
                if (value is string stringValue)
                {
                    return Regex.IsMatch(stringValue, pattern);
                }
                return false;
            });
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
                null
            ));

            return this;
        }

        /// <summary>
        /// Fails unless the value is a <see cref="DateTime"/> strictly later than <see cref="DateTime.Now"/>.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsDateInFuture()
        {
            return Must(value =>
            {
                if (value is DateTime date)
                {
                    return date > DateTime.Now;
                }
                return false;
            });
        }

        /// <summary>
        /// Fails unless the value is a non-string <see cref="System.Collections.IEnumerable"/> whose
        /// items are all distinct. Non-collection values always fail.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsUnique()
        {
            return Must(value =>
            {
                if (value is not string && value is System.Collections.IEnumerable collection)
                {
                    var items = collection.Cast<object>().ToList();
                    return items.Distinct().Count() == items.Count;
                }
                return false;
            });
        }

        /// <summary>
        /// Fails unless the value converts to <see cref="int"/> and is strictly less than <paramref name="maxValue"/>.
        /// A value that cannot be converted to <see cref="int"/> fails as well.
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound.</param>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsLessThan(int maxValue)
        {
            return Must(value => TryConvertToInt32(value, out var intValue) && intValue < maxValue);
        }

        /// <summary>
        /// Fails unless the value is a <see cref="DateTime"/> strictly earlier than <see cref="DateTime.Now"/>.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsDateInPast()
        {
            return Must(value =>
            {
                if (value is DateTime date)
                {
                    return date < DateTime.Now;
                }
                return false;
            });
        }

        /// <summary>
        /// Fails unless the value is a <see cref="DateTime"/> strictly later than <see cref="DateTime.Now"/>.
        /// Equivalent to <see cref="IsDateInFuture"/>.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsInFuture()
        {
            return Must(value =>
            {
                if (value is DateTime date)
                {
                    return date > DateTime.Now;
                }
                return false;
            });
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
            return Must(value =>
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
            });
        }

        /// <summary>
        /// Fails unless the value's string representation reads the same forwards and backwards
        /// (case-insensitive). A <see langword="null"/> value or one that stringifies to <see langword="null"/> passes.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public ValidationRuleBuilder<T, TProperty> IsPalindrome()
        {
            return Must(value =>
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
            });
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
                null
            ));

            return this;
        }

        /// <summary>
        /// <see langword="true"/> if any rule added via <see cref="MustAsync"/> or a <c>ShouldAsync</c>
        /// overload has been registered on this property.
        /// </summary>
        public bool HasAsyncRules => _rulesWithMessages.Any(r =>
            r.rule is Func<TProperty, Task<bool>> ||
            r.rule is Func<T, TProperty, ValidationResult, Task<bool>>);
    }
}
