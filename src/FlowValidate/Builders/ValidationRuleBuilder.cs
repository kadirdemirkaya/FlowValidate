using FlowValidate.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowValidate.Builders
{
    public class ValidationRuleBuilder<T, TProperty>
    {
        private StringBuilder _shouldBuilder;
        private StringBuilder _shouldListBuilder;
        private readonly Expression<Func<T, TProperty>> _property;
        private readonly Func<T, TProperty> _propertyFunc;
        private readonly string _propertyName;
        private readonly List<(Delegate rule, ValidationFailure? validationFailure, bool isFromShould)> _rulesWithMessages = new();

        public ValidationRuleBuilder(Expression<Func<T, TProperty>> property)
        {
            _property = property;
            _propertyFunc = _property.Compile();
            _propertyName = GetPropertyName(property);
            _shouldBuilder = new StringBuilder();
            _shouldListBuilder = new StringBuilder();
        }

        private static string GetPropertyName(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberExpr)
                return memberExpr.Member.Name;

            return null;
        }

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
                    (lastRule.rule, new ValidationFailure(
                        propertyName: _propertyName,
                        errorMessage: defaultMessage,
                        attemptedValue: null,
                        errorCode: defaultCode
                    ), lastRule.isFromShould);
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
                    ), lastRule.isFromShould);
            }

            return this;
        }


        public Task<ValidationResult> ValidateAsync(T instance)
        {
            var result = new ValidationResult();
            var value = _propertyFunc(instance);
            bool isValid = true;

            foreach (var (rule, validationFailure, isFromShould) in _rulesWithMessages)
            {
                if (result.SkipRemainingRules) break;

                bool passed = rule switch
                {
                    Func<TProperty, bool> r1 => r1(value),
                    Func<T, TProperty, ValidationResult, bool> r2 => r2(instance, value, result),
                    _ => true
                };

                if (!passed)
                {
                    if (!passed)
                    {
                        var failure = validationFailure ?? new ValidationFailure(
                            propertyName: _propertyName,
                            errorMessage: "Validation failed for property.",
                            attemptedValue: value,
                            errorCode: "DefaultRule"
                        );

                        result.AddFailure(failure);
                        result.SetIsValid(false);
                    }
                }
            }

            _shouldBuilder.Clear();
            _shouldListBuilder.Clear();

            return Task.FromResult(result);
        }

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
                  false
              ));

            return this;
        }

        public ValidationRuleBuilder<T, TProperty> Must(Func<TProperty, bool> rule)
        {
            _rulesWithMessages.Add((rule, null, false));
            return this;
        }

        public ValidationRuleBuilder<T, TProperty> IsNotEmpty()
        {
            return Must(value =>
            {
                if (value is string str)
                    return !string.IsNullOrWhiteSpace(str);

                return value != null;
            });
        }

        public ValidationRuleBuilder<T, TProperty> IsEqual(TProperty expectedValue)
        {
            return Must(value => EqualityComparer<TProperty>.Default.Equals(value, expectedValue));
        }

        public ValidationRuleBuilder<T, TProperty> Contains(string substring)
        {
            return Must(value => value.ToString().Contains(substring));
        }

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

        public ValidationRuleBuilder<T, TProperty> Length(int minLength, int maxLength)
        {
            return Must(value => value.ToString().Length >= minLength && value.ToString().Length <= maxLength);
        }

        public ValidationRuleBuilder<T, TProperty> IsGreaterThan(int minValue)
        {
            return Must(value => Convert.ToInt32(value) > minValue);
        }

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

        public ValidationRuleBuilder<T, TProperty> IsUnique()
        {
            return Must(value =>
            {
                if (value is IEnumerable<object> collection)
                {
                    return collection.Distinct().Count() == collection.Count();
                }
                return false;
            });
        }

        public ValidationRuleBuilder<T, TProperty> IsLessThan(int maxValue)
        {
            return Must(value => Convert.ToInt32(value) < maxValue);
        }

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

        public ValidationRuleBuilder<T, TProperty> NoConsecutiveRepeats(int maxRepeatLength = 3)
        {
            return Must(value =>
            {
                if (value == null) return true;

                string str = value.ToString();
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

        public ValidationRuleBuilder<T, TProperty> IsPalindrome()
        {
            return Must(value =>
            {
                if (value == null)
                    return true;

                string str = value.ToString().ToLowerInvariant();

                int length = str.Length;
                for (int i = 0; i < length / 2; i++)
                {
                    if (str[i] != str[length - 1 - i])
                        return false;
                }

                return true;
            });
        }

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
                true
            ));

            return this;
        }

        public string GetAllErrors() => _shouldBuilder.ToString();

        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty> action, string errorMessage = null)
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
                true
            ));

            return this;
        }


        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty> action, params string[] errorMessages)
        {
            _rulesWithMessages.Add((
                (Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
                {
                    try
                    {
                        action(value);

                        if (errorMessages != null && errorMessages.Length > 0)
                        {
                            foreach (var msg in errorMessages)
                            {
                                result.AddFailure(new ValidationFailure(
                                    propertyName: _propertyName,
                                    errorMessage: msg,
                                    attemptedValue: value,
                                    errorCode: "ShouldRule"
                                ));
                            }
                        }

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
                true
            ));

            return this;
        }

        private bool AnyListErrors => _shouldListBuilder.Length > 0 ? false : true;
    }
}
