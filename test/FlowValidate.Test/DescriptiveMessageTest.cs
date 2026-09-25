using FlowValidate.Builders;
using FlowValidate.Enums;
using FlowValidate.Models;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace FlowValidate.Test
{
    public class DescriptiveMessageTest
    {
        private const string DefaultMessage = "Validation failed for property.";

        private const string CatastrophicPattern = "^(a+)+$";

        private static readonly string CatastrophicInput = new string('a', 40) + "!";

        private static readonly Regex DigitsPattern = new Regex(@"^\d+$");

        private class Sample
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Code { get; set; }
            public string? Word { get; set; }
            public string? Password { get; set; }
            public int Age { get; set; }
            public decimal Price { get; set; }
            public decimal? Discount { get; set; }
            public DateTime StartsAt { get; set; }
            public DateTime EndedAt { get; set; }
            public DateTime Renewal { get; set; }
            public List<int> Tags { get; set; } = new();
        }

        private class SampleValidator : BaseValidator<Sample>
        {
        }

        private class RuleCase
        {
            public RuleCase(Func<bool, ValidationFailure> run, string message, string code)
            {
                Run = run;
                Message = message;
                Code = code;
            }

            public Func<bool, ValidationFailure> Run { get; }

            public string Message { get; }

            public string Code { get; }
        }

        private static ValidationFailure ValidateSingle<TProperty>(
            Sample model,
            Expression<Func<Sample, TProperty>> property,
            Action<ValidationRuleBuilder<Sample, TProperty>> configureRule,
            bool descriptive)
        {
            var validator = new SampleValidator();

            if (descriptive)
                validator.UseDescriptiveMessages();

            configureRule(validator.RuleFor(property));

            var result = validator.Validate(model);

            Assert.False(result.IsValid);

            return Assert.Single(result.Failures);
        }

        private static readonly Dictionary<string, RuleCase> Cases = new()
        {
            ["IsNotEmpty"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Name = "   " }, x => x.Name, rule => rule.IsNotEmpty(), descriptive),
                "Name must not be empty.",
                BuiltInRuleCodes.NotEmpty),

            ["IsEqual"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Name = "other" }, x => x.Name, rule => rule.IsEqual("expected"), descriptive),
                "Name must be equal to 'expected'.",
                BuiltInRuleCodes.Equal),

            ["Contains"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Name = "xyz" }, x => x.Name, rule => rule.Contains("abc"), descriptive),
                "Name must contain 'abc'.",
                BuiltInRuleCodes.Contains),

            ["IsInRange(int)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Age = 5 }, x => x.Age, rule => rule.IsInRange(18, 65), descriptive),
                "Age must be between 18 and 65.",
                BuiltInRuleCodes.InRange),

            ["IsEmail"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Email = "not-an-email" }, x => x.Email, rule => rule.IsEmail(), descriptive),
                "Email must be a valid email address.",
                BuiltInRuleCodes.Email),

            ["Length"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Name = "ab" }, x => x.Name, rule => rule.Length(3, 100), descriptive),
                "Name must be between 3 and 100 characters.",
                BuiltInRuleCodes.Length),

            ["IsGreaterThan(int)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Age = 5 }, x => x.Age, rule => rule.IsGreaterThan(18), descriptive),
                "Age must be greater than 18.",
                BuiltInRuleCodes.GreaterThan),

            ["IsLessThan(int)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Age = 90 }, x => x.Age, rule => rule.IsLessThan(65), descriptive),
                "Age must be less than 65.",
                BuiltInRuleCodes.LessThan),

            ["MatchesRegex(string)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Code = "abc" }, x => x.Code, rule => rule.MatchesRegex(@"^\d+$"), descriptive),
                "Code must match the required pattern.",
                BuiltInRuleCodes.RegexMatch),

            ["MatchesRegex(string, TimeSpan)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Code = "abc" }, x => x.Code, rule => rule.MatchesRegex(@"^\d+$", TimeSpan.FromSeconds(1)), descriptive),
                "Code must match the required pattern.",
                BuiltInRuleCodes.RegexMatch),

            ["MatchesRegex(Regex)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Code = "abc" }, x => x.Code, rule => rule.MatchesRegex(DigitsPattern), descriptive),
                "Code must match the required pattern.",
                BuiltInRuleCodes.RegexMatch),

            ["IsDateInFuture"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { StartsAt = DateTime.Now.AddDays(-1) }, x => x.StartsAt, rule => rule.IsDateInFuture(), descriptive),
                "StartsAt must be a date in the future.",
                BuiltInRuleCodes.DateInFuture),

            ["IsDateInPast"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { EndedAt = DateTime.Now.AddDays(1) }, x => x.EndedAt, rule => rule.IsDateInPast(), descriptive),
                "EndedAt must be a date in the past.",
                BuiltInRuleCodes.DateInPast),

            ["IsInFuture"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Renewal = DateTime.Now.AddDays(-1) }, x => x.Renewal, rule => rule.IsInFuture(), descriptive),
                "Renewal must be a date in the future.",
                BuiltInRuleCodes.InFuture),

            ["IsUnique"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Tags = new List<int> { 1, 1 } }, x => x.Tags, rule => rule.IsUnique(), descriptive),
                "Tags must contain unique items.",
                BuiltInRuleCodes.Unique),

            ["NoConsecutiveRepeats"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Password = "aaa" }, x => x.Password, rule => rule.NoConsecutiveRepeats(), descriptive),
                "Password must not repeat the same character 3 or more times in a row.",
                BuiltInRuleCodes.NoConsecutiveRepeats),

            ["IsPalindrome"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Word = "abc" }, x => x.Word, rule => rule.IsPalindrome(), descriptive),
                "Word must be a palindrome.",
                BuiltInRuleCodes.Palindrome),

            ["IsInRange(IComparable)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Price = 50m }, x => x.Price, rule => rule.IsInRange(1m, 10m), descriptive),
                "Price must be between 1 and 10.",
                BuiltInRuleCodes.InRange),

            ["IsInRange(nullable IComparable)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Discount = null }, x => x.Discount, rule => rule.IsInRange(0m, 5m), descriptive),
                "Discount must be between 0 and 5.",
                BuiltInRuleCodes.InRange),

            ["IsGreaterThan(IComparable)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Price = 50m }, x => x.Price, rule => rule.IsGreaterThan(100m), descriptive),
                "Price must be greater than 100.",
                BuiltInRuleCodes.GreaterThan),

            ["IsGreaterThan(nullable IComparable)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Discount = null }, x => x.Discount, rule => rule.IsGreaterThan(0m), descriptive),
                "Discount must be greater than 0.",
                BuiltInRuleCodes.GreaterThan),

            ["IsLessThan(IComparable)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Price = 50m }, x => x.Price, rule => rule.IsLessThan(10m), descriptive),
                "Price must be less than 10.",
                BuiltInRuleCodes.LessThan),

            ["IsLessThan(nullable IComparable)"] = new RuleCase(
                descriptive => ValidateSingle(new Sample { Discount = null }, x => x.Discount, rule => rule.IsLessThan(1m), descriptive),
                "Discount must be less than 1.",
                BuiltInRuleCodes.LessThan)
        };

        public static IEnumerable<object[]> BuiltInRules => Cases.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(BuiltInRules))]
        public void DescriptiveMessagesOff_KeepsTheDefaultMessageAndCode(string ruleName)
        {
            var failure = Cases[ruleName].Run(false);

            Assert.Equal(DefaultMessage, failure.ErrorMessage);
            Assert.Equal("DefaultRule", failure.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(BuiltInRules))]
        public void DescriptiveMessagesOn_ReportsTheRuleMessageAndCode(string ruleName)
        {
            var ruleCase = Cases[ruleName];

            var failure = ruleCase.Run(true);

            Assert.Equal(ruleCase.Message, failure.ErrorMessage);
            Assert.Equal(ruleCase.Code, failure.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessagesOn_KeepsTheAttemptedValueAndSeverity()
        {
            var failure = ValidateSingle(new Sample { Name = "ab" }, x => x.Name, rule => rule.Length(3, 100), true);

            Assert.Equal("Name", failure.PropertyName);
            Assert.Equal("ab", failure.AttemptedValue);
            Assert.Equal(Severity.Error, failure.Severity);
        }

        [Fact]
        public void DescriptiveMessagesOn_ComposesWithWithSeverity()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Length(3, 100).WithSeverity(Severity.Warning),
                true);

            Assert.Equal("Name must be between 3 and 100 characters.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Length, failure.ErrorCode);
            Assert.Equal(Severity.Warning, failure.Severity);
        }

        [Fact]
        public void WithMessage_OverridesTheDescriptiveMessage_AndKeepsTheRuleCode()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Length(3, 100).WithMessage("Name is too short."),
                true);

            Assert.Equal("Name is too short.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Length, failure.ErrorCode);
        }

        [Fact]
        public void WithMessage_OverridesTheDescriptiveMessageAndCode_WhenACodeIsGiven()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Length(3, 100).WithMessage("Name is too short.", "NAME_LENGTH"),
                true);

            Assert.Equal("Name is too short.", failure.ErrorMessage);
            Assert.Equal("NAME_LENGTH", failure.ErrorCode);
        }

        [Fact]
        public void WithMessage_WithoutACode_StillReportsDefaultRule_WhenDescriptiveMessagesAreOff()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Length(3, 100).WithMessage("Name is too short."),
                false);

            Assert.Equal("Name is too short.", failure.ErrorMessage);
            Assert.Equal("DefaultRule", failure.ErrorCode);
        }

        [Fact]
        public void WithMessage_WithACode_IsUnchanged_WhenDescriptiveMessagesAreOff()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Length(3, 100).WithMessage("Name is too short.", "NAME_LENGTH"),
                false);

            Assert.Equal("Name is too short.", failure.ErrorMessage);
            Assert.Equal("NAME_LENGTH", failure.ErrorCode);
        }

        [Fact]
        public void WithDescriptiveMessages_OptsInASingleChain_WithoutTouchingTheValidator()
        {
            var validator = new SampleValidator();
            validator.RuleFor(x => x.Name).Length(3, 100).WithDescriptiveMessages();
            validator.RuleFor(x => x.Email).IsEmail();

            var result = validator.Validate(new Sample { Name = "ab", Email = "nope" });

            Assert.Equal(2, result.Failures.Count);

            var name = result.Failures.Single(f => f.PropertyName == "Name");
            var email = result.Failures.Single(f => f.PropertyName == "Email");

            Assert.Equal("Name must be between 3 and 100 characters.", name.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Length, name.ErrorCode);
            Assert.Equal(DefaultMessage, email.ErrorMessage);
            Assert.Equal("DefaultRule", email.ErrorCode);
        }

        [Fact]
        public void WithDescriptiveMessagesFalse_OptsASingleChainOutOfTheValidatorSetting()
        {
            var validator = new SampleValidator();
            validator.UseDescriptiveMessages();
            validator.RuleFor(x => x.Name).Length(3, 100).WithDescriptiveMessages(false);
            validator.RuleFor(x => x.Email).IsEmail();

            var result = validator.Validate(new Sample { Name = "ab", Email = "nope" });

            var name = result.Failures.Single(f => f.PropertyName == "Name");
            var email = result.Failures.Single(f => f.PropertyName == "Email");

            Assert.Equal(DefaultMessage, name.ErrorMessage);
            Assert.Equal("DefaultRule", name.ErrorCode);
            Assert.Equal("Email must be a valid email address.", email.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Email, email.ErrorCode);
        }

        [Fact]
        public void WithDescriptiveMessages_AppliesToRulesAddedBeforeAndAfterIt()
        {
            var validator = new SampleValidator();
            validator.RuleFor(x => x.Name).Length(3, 100).WithDescriptiveMessages().IsEqual("expected");

            var result = validator.Validate(new Sample { Name = "ab" });

            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("Name must be between 3 and 100 characters.", result.Failures[0].ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Length, result.Failures[0].ErrorCode);
            Assert.Equal("Name must be equal to 'expected'.", result.Failures[1].ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Equal, result.Failures[1].ErrorCode);
        }

        [Fact]
        public void UseDescriptiveMessages_AppliesToChainsRegisteredBeforeTheCall()
        {
            var validator = new SampleValidator();
            validator.RuleFor(x => x.Name).IsNotEmpty();
            validator.UseDescriptiveMessages();

            var result = validator.Validate(new Sample { Name = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal("Name must not be empty.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, failure.ErrorCode);
        }

        [Fact]
        public void UseDescriptiveMessagesFalse_SwitchesTheValidatorBackToTheDefaults()
        {
            var validator = new SampleValidator();
            validator.UseDescriptiveMessages();
            validator.RuleFor(x => x.Name).IsNotEmpty();
            validator.UseDescriptiveMessages(false);

            var result = validator.Validate(new Sample { Name = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(DefaultMessage, failure.ErrorMessage);
            Assert.Equal("DefaultRule", failure.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessagesOn_LeavesRequiredIfUnchanged()
        {
            var failure = ValidateSingle(
                new Sample { Name = null },
                x => x.Name,
                rule => rule.RequiredIf(value => value != null),
                true);

            Assert.Equal("Property is required.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Required, failure.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessagesOn_LeavesMustUnchanged()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Must(value => value == "expected"),
                true);

            Assert.Equal(DefaultMessage, failure.ErrorMessage);
            Assert.Equal("DefaultRule", failure.ErrorCode);
        }

        [Fact]
        public async Task DescriptiveMessagesOn_LeavesMustAsyncUnchanged()
        {
            var validator = new SampleValidator();
            validator.UseDescriptiveMessages();
            validator.RuleFor(x => x.Name).MustAsync(value => Task.FromResult(value == "expected"));

            var result = await validator.ValidateAsync(new Sample { Name = "ab" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(DefaultMessage, failure.ErrorMessage);
            Assert.Equal("DefaultRule", failure.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessagesOn_LeavesShouldUnchanged()
        {
            var failure = ValidateSingle(
                new Sample { Name = "ab" },
                x => x.Name,
                rule => rule.Should((value, error) => error("Name is not acceptable.")),
                true);

            Assert.Equal("Name is not acceptable.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.ShouldRule, failure.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessagesOn_KeepsTheRegexTimeoutFailure()
        {
            var validator = new SampleValidator();
            validator.UseDescriptiveMessages();
            validator.RuleFor(x => x.Code).MatchesRegex(CatastrophicPattern, TimeSpan.FromMilliseconds(100));

            var result = validator.Validate(new Sample { Code = CatastrophicInput });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(BuiltInRuleCodes.RegexTimeout, failure.ErrorCode);
            Assert.StartsWith("Regular expression match timed out after", failure.ErrorMessage);
        }

        private class Order
        {
            public string? Reference { get; set; }
            public Customer? Customer { get; set; }
            public List<Line> Lines { get; set; } = new();
        }

        private class Customer
        {
            public string? Name { get; set; }
        }

        private class Line
        {
            public string? Sku { get; set; }
        }

        private class CustomerValidator : BaseValidator<Customer>
        {
            public CustomerValidator(bool descriptive)
            {
                if (descriptive)
                    UseDescriptiveMessages();

                RuleFor(x => x.Name).IsNotEmpty();
            }
        }

        private class LineValidator : BaseValidator<Line>
        {
            public LineValidator(bool descriptive)
            {
                if (descriptive)
                    UseDescriptiveMessages();

                RuleFor(x => x.Sku).Length(3, 10);
            }
        }

        private class OrderValidator : BaseValidator<Order>
        {
            public OrderValidator(bool descriptive, CustomerValidator customerValidator, LineValidator lineValidator)
            {
                if (descriptive)
                    UseDescriptiveMessages();

                RuleFor(x => x.Reference).IsNotEmpty();
                ValidateNested(x => x.Customer, customerValidator);
                ValidateCollection(x => x.Lines, lineValidator, line => line);
            }
        }

        private static Order InvalidOrder() => new Order
        {
            Reference = "",
            Customer = new Customer { Name = "" },
            Lines = new List<Line> { new Line { Sku = "ab" } }
        };

        [Fact]
        public void DescriptiveMessages_ReachNestedAndCollectionValidatorsThatOptIn()
        {
            var validator = new OrderValidator(true, new CustomerValidator(true), new LineValidator(true));

            var result = validator.Validate(InvalidOrder());

            Assert.Equal(3, result.Failures.Count);

            var reference = result.Failures.Single(f => f.PropertyName == "Reference");
            var name = result.Failures.Single(f => f.PropertyName == "Name");
            var sku = result.Failures.Single(f => f.PropertyName == "Sku");

            Assert.Equal("Reference must not be empty.", reference.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, reference.ErrorCode);

            Assert.Equal("Name must not be empty.", name.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, name.ErrorCode);

            Assert.Equal("Element 1: Sku must be between 3 and 10 characters.", sku.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.Length, sku.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessages_AreNotPropagatedToNestedAndCollectionValidators()
        {
            var validator = new OrderValidator(true, new CustomerValidator(false), new LineValidator(false));

            var result = validator.Validate(InvalidOrder());

            var name = result.Failures.Single(f => f.PropertyName == "Name");
            var sku = result.Failures.Single(f => f.PropertyName == "Sku");

            Assert.Equal(DefaultMessage, name.ErrorMessage);
            Assert.Equal("DefaultRule", name.ErrorCode);

            Assert.Equal($"Element 1: {DefaultMessage}", sku.ErrorMessage);
            Assert.Equal("DefaultRule", sku.ErrorCode);
        }

        [Fact]
        public void DescriptiveMessagesOff_KeepsTheWholeCompositionUnchanged()
        {
            var validator = new OrderValidator(false, new CustomerValidator(false), new LineValidator(false));

            var result = validator.Validate(InvalidOrder());

            Assert.Equal(3, result.Failures.Count);
            Assert.All(result.Failures, failure => Assert.Equal("DefaultRule", failure.ErrorCode));
            Assert.Equal(DefaultMessage, result.Failures.Single(f => f.PropertyName == "Reference").ErrorMessage);
            Assert.Equal(DefaultMessage, result.Failures.Single(f => f.PropertyName == "Name").ErrorMessage);
            Assert.Equal($"Element 1: {DefaultMessage}", result.Failures.Single(f => f.PropertyName == "Sku").ErrorMessage);
        }
    }
}
