using FlowValidate.Builders;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace FlowValidate.Test
{
    public class RegexMatchTimeoutTest
    {
        private const string CatastrophicPattern = "^(a+)+$";

        private static readonly string CatastrophicInput = new string('a', 40) + "!";

        private class Entry
        {
            public string? Phone { get; set; }
            public int Code { get; set; }
        }

        private class EntryValidator : BaseValidator<Entry>
        {
        }

        private static ValidationResult Validate<TProperty>(
            Entry model,
            Expression<Func<Entry, TProperty>> property,
            Action<ValidationRuleBuilder<Entry, TProperty>> configureRule)
        {
            var validator = new EntryValidator();
            configureRule(validator.RuleFor(property));
            return validator.Validate(model);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldPass_ForMatchingValue()
        {
            // Act
            var result = Validate(
                new Entry { Phone = "5551234567" },
                x => x.Phone,
                rule => rule.MatchesRegex(@"^\d{10}$", TimeSpan.FromSeconds(1)));

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldFail_ForNonMatchingValue()
        {
            // Act
            var result = Validate(
                new Entry { Phone = "555" },
                x => x.Phone,
                rule => rule.MatchesRegex(@"^\d{10}$", TimeSpan.FromSeconds(1)));

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Phone", failure.PropertyName);
            Assert.Equal("DefaultRule", failure.ErrorCode);
            Assert.Equal("Validation failed for property.", failure.ErrorMessage);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldFailLikeTheUntimedOverload_ForNullValue()
        {
            // Act
            var withoutTimeout = Validate(
                new Entry { Phone = null },
                x => x.Phone,
                rule => rule.MatchesRegex(@"^\d{10}$"));

            var withTimeout = Validate(
                new Entry { Phone = null },
                x => x.Phone,
                rule => rule.MatchesRegex(@"^\d{10}$", TimeSpan.FromSeconds(1)));

            // Assert
            Assert.False(withoutTimeout.IsValid);
            Assert.False(withTimeout.IsValid);

            var expected = Assert.Single(withoutTimeout.Failures);
            var actual = Assert.Single(withTimeout.Failures);
            Assert.Equal(expected.PropertyName, actual.PropertyName);
            Assert.Equal(expected.ErrorMessage, actual.ErrorMessage);
            Assert.Equal(expected.ErrorCode, actual.ErrorCode);
            Assert.Equal(expected.AttemptedValue, actual.AttemptedValue);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldFailLikeTheUntimedOverload_ForNonStringValue()
        {
            // Act
            var withoutTimeout = Validate(
                new Entry { Code = 42 },
                x => x.Code,
                rule => rule.MatchesRegex(@"^\d+$"));

            var withTimeout = Validate(
                new Entry { Code = 42 },
                x => x.Code,
                rule => rule.MatchesRegex(@"^\d+$", TimeSpan.FromSeconds(1)));

            // Assert
            Assert.False(withoutTimeout.IsValid);
            Assert.False(withTimeout.IsValid);

            var expected = Assert.Single(withoutTimeout.Failures);
            var actual = Assert.Single(withTimeout.Failures);
            Assert.Equal(expected.PropertyName, actual.PropertyName);
            Assert.Equal(expected.ErrorMessage, actual.ErrorMessage);
            Assert.Equal(expected.ErrorCode, actual.ErrorCode);
            Assert.Equal(expected.AttemptedValue, actual.AttemptedValue);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldReportRuleFailure_ForCatastrophicPattern()
        {
            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = Validate(
                new Entry { Phone = CatastrophicInput },
                x => x.Phone,
                rule => rule.MatchesRegex(CatastrophicPattern, TimeSpan.FromMilliseconds(100)));
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Validation took {stopwatch.Elapsed}.");
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Phone", failure.PropertyName);
            Assert.Equal("RegexTimeout", failure.ErrorCode);
            Assert.Contains("timed out", failure.ErrorMessage);
            Assert.Equal(CatastrophicInput, failure.AttemptedValue);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldKeepTheTimeoutMessage_WhenWithMessageIsChained()
        {
            // Act
            var result = Validate(
                new Entry { Phone = CatastrophicInput },
                x => x.Phone,
                rule => rule.MatchesRegex(CatastrophicPattern, TimeSpan.FromMilliseconds(100))
                            .WithMessage("Phone is invalid.", "PhoneFormat"));

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("RegexTimeout", failure.ErrorCode);
            Assert.Contains("timed out", failure.ErrorMessage);
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldStillHonorWithMessage_ForAnOrdinaryNoMatch()
        {
            // Act
            var result = Validate(
                new Entry { Phone = "555" },
                x => x.Phone,
                rule => rule.MatchesRegex(@"^\d{10}$", TimeSpan.FromSeconds(1))
                            .WithMessage("Phone is invalid.", "PhoneFormat"));

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("PhoneFormat", failure.ErrorCode);
            Assert.Equal("Phone is invalid.", failure.ErrorMessage);
        }

        [Fact]
        public void MatchesRegexInstance_ShouldPassAndFail_AccordingToItsOwnOptions()
        {
            // Arrange
            var regex = new Regex("^abc$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

            // Act
            var matching = Validate(new Entry { Phone = "ABC" }, x => x.Phone, rule => rule.MatchesRegex(regex));
            var nonMatching = Validate(new Entry { Phone = "abcd" }, x => x.Phone, rule => rule.MatchesRegex(regex));

            // Assert
            Assert.True(matching.IsValid);
            Assert.Empty(matching.Failures);

            Assert.False(nonMatching.IsValid);
            Assert.Equal("DefaultRule", Assert.Single(nonMatching.Failures).ErrorCode);
        }

        [Fact]
        public void MatchesRegexInstance_ShouldFailLikeTheUntimedOverload_ForNullValue()
        {
            // Arrange
            var regex = new Regex(@"^\d{10}$", RegexOptions.None, TimeSpan.FromSeconds(1));

            // Act
            var withoutTimeout = Validate(new Entry { Phone = null }, x => x.Phone, rule => rule.MatchesRegex(@"^\d{10}$"));
            var withRegex = Validate(new Entry { Phone = null }, x => x.Phone, rule => rule.MatchesRegex(regex));

            // Assert
            Assert.False(withoutTimeout.IsValid);
            Assert.False(withRegex.IsValid);

            var expected = Assert.Single(withoutTimeout.Failures);
            var actual = Assert.Single(withRegex.Failures);
            Assert.Equal(expected.PropertyName, actual.PropertyName);
            Assert.Equal(expected.ErrorMessage, actual.ErrorMessage);
            Assert.Equal(expected.ErrorCode, actual.ErrorCode);
        }

        [Fact]
        public void MatchesRegexInstance_ShouldReportRuleFailure_ForCatastrophicPattern()
        {
            // Arrange
            var regex = new Regex(CatastrophicPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = Validate(new Entry { Phone = CatastrophicInput }, x => x.Phone, rule => rule.MatchesRegex(regex));
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Validation took {stopwatch.Elapsed}.");
            Assert.False(result.IsValid);
            Assert.Equal("RegexTimeout", Assert.Single(result.Failures).ErrorCode);
        }

        [Fact]
        public void MatchesRegexInstance_ShouldThrowArgumentNullException_ForNullRegex()
        {
            // Arrange
            var validator = new EntryValidator();
            var builder = validator.RuleFor(x => x.Phone);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => builder.MatchesRegex((Regex)null!));
        }

        [Fact]
        public void MatchesRegexWithTimeout_ShouldNotAffectTheUntimedOverload_ForACheapPattern()
        {
            // Act
            var result = Validate(
                new Entry { Phone = "5551234567" },
                x => x.Phone,
                rule => rule.MatchesRegex(@"^\d{10}$"));

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
        }
    }
}
