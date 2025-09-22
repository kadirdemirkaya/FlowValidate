using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowValidate.Builders
{
    public class ValidationRuleBuilder<T, TProperty>
    {
        private StringBuilder _shouldBuilder;
        private StringBuilder _shouldListBuilder;
        private readonly Func<T, TProperty> _property;
        //private readonly List<(Func<TProperty, bool> rule, string errorMessage, bool isFromShould)> _rulesWithMessages = new();
        private readonly List<(Delegate rule, string errorMessage, bool isFromShould)> _rulesWithMessages = new();

        private readonly List<(Func<T, bool> rule, string errorMessage)> _dependentRulesWithMessages = new();

        public ValidationRuleBuilder(Func<T, TProperty> property)
        {
            _property = property;
            _shouldBuilder = new StringBuilder();
            _shouldListBuilder = new StringBuilder();
        }

        public ValidationRuleBuilder<T, TProperty> WithMessage(string errorMessage)
        {
            if (_rulesWithMessages.Count == 0)
            {
                throw new InvalidOperationException("No rules are defined. Please define a rule before setting an error message.");
            }

            var lastRule = _rulesWithMessages.Last();

            if (lastRule.isFromShould)
            {
                _rulesWithMessages[_rulesWithMessages.Count - 1] = (lastRule.rule, errorMessage, lastRule.isFromShould);
            }
            else
            {
                _rulesWithMessages[_rulesWithMessages.Count - 1] = (lastRule.rule, errorMessage, false);
            }

            return this;
        }

        public ValidationRuleBuilder<T, TProperty> AddDependentRule(Func<T, bool> rule, string errorMessage)
        {
            _dependentRulesWithMessages.Add((rule, errorMessage));
            return this;
        }

        public Task<ValidationResult> ValidateAsync(T instance)
        {
            var result = new ValidationResult();
            var value = _property(instance);
            bool isValid = true;

            foreach (var (rule, errorMessage, isFromShould) in _rulesWithMessages)
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
                    if (isFromShould)
                    {
                        string shouldError = GetAllErrors();
                        result.AddError(errorMessage ?? "Validation should failed for property.");
                        result.AddError(shouldError ?? "Validation should failed for property.");
                        result.SetIsValid(false);
                    }
                    else
                    {
                        result.AddError(errorMessage ?? "Validation failed for property.");
                        result.SetIsValid(false);
                    }
                }
            }


            if (isValid)
                foreach (var (rule, errorMessage) in _dependentRulesWithMessages)
                {
                    if (!rule(instance))
                    {
                        result.AddError(errorMessage);
                        result.SetIsValid(false);
                    }
                }

            _shouldBuilder.Clear();
            _shouldListBuilder.Clear();

            return Task.FromResult(result);
        }

        public ValidationRuleBuilder<T, TProperty> RequiredIf(Func<TProperty, bool> condition)
        {
            _rulesWithMessages.Add(((Func<T, TProperty, ValidationResult, bool>)((instance, value, result) =>
            {
                if (!condition(value))
                {
                    result.SetSkipRemainingRules(true);
                    return false; // geçer say
                }

                if (value is string str)
                    return !string.IsNullOrWhiteSpace(str);

                return value != null;

            }), "Property is required.", false));

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
            Action<string> addError = error => AddShouldError(error);
            string errors = string.Empty;

            _rulesWithMessages.Add((
                (Func<TProperty, bool>)(value =>
                {
                    try
                    {
                        action(value, addError);
                        errors = _shouldBuilder.ToString();
                        return string.IsNullOrEmpty(_shouldBuilder.ToString());
                    }
                    catch
                    {
                        return false;
                    }
                }), "Should custom error", true));

            return this;
        }
        private void AddShouldError(string error)
        {
            if (_shouldBuilder.Length > 0)
            {
                _shouldBuilder.Append(" & ");
            }
            _shouldBuilder.Append(error);
        }
        public string GetAllErrors() => _shouldBuilder.ToString();

        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty> action, string errorMessage = null)
        {
            _rulesWithMessages.Add((
                (Func<TProperty, bool>)(value =>
                {
                    try
                    {
                        action(value);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }), errorMessage ?? "Custom validation failed !.", true));

            return this;
        }

        public ValidationRuleBuilder<T, TProperty> Should(Action<TProperty> action, params string[] errorMessage)
        {
            if (errorMessage.Length > 0)
                ErrorMessageToList(errorMessage);

            _rulesWithMessages.Add((
                (Func<TProperty, bool>)(value =>
            {
                try
                {
                    action(value);
                    return AnyListErrors;
                }
                catch
                {
                    return false;
                }
            }), _shouldListBuilder.ToString() ?? "Custom validation failed !", true));

            return this;
        }
        private void ErrorMessageToList(string[] errors)
        {
            foreach (var err in errors)
            {
                if (_shouldListBuilder.Length > 0)
                {
                    _shouldListBuilder.Append(" & ");
                }
                _shouldListBuilder.Append(err);
            }
        }

        private bool AnyListErrors => _shouldListBuilder.Length > 0 ? false : true;
    }
}
