using FlowValidate.Builders;
using System.Linq.Expressions;

namespace FlowValidate.Test
{
    public class IntOverloadConversionTest
    {
        private class Reading
        {
            public decimal Price { get; set; }
            public long Views { get; set; }
            public string? Code { get; set; }
            public DateTime CreatedAt { get; set; }
            public int? Stock { get; set; }
        }

        private class ReadingValidator : BaseValidator<Reading>
        {
        }

        private static ValidationResult Validate<TProperty>(
            Reading model,
            Expression<Func<Reading, TProperty>> property,
            Action<ValidationRuleBuilder<Reading, TProperty>> configureRule)
        {
            var validator = new ReadingValidator();
            configureRule(validator.RuleFor(property));
            return validator.Validate(model);
        }

        [Fact]
        public void IsGreaterThan_ShouldNotThrow_AndKeepTruncation_ForDecimalValue()
        {
            // Act
            var result = Validate(new Reading { Price = 5.4m }, x => x.Price, rule => rule.IsGreaterThan(5));

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Price", Assert.Single(result.Failures).PropertyName);
        }

        [Fact]
        public void IsLessThan_ShouldNotThrow_AndKeepRounding_ForDecimalValue()
        {
            // Act
            var result = Validate(new Reading { Price = 5.6m }, x => x.Price, rule => rule.IsLessThan(6));

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Price", Assert.Single(result.Failures).PropertyName);
        }

        [Fact]
        public void IsGreaterThan_ShouldReportFailure_InsteadOfOverflowException_ForOutOfRangeLong()
        {
            // Act
            var result = Validate(new Reading { Views = 3_000_000_000L }, x => x.Views, rule => rule.IsGreaterThan(0));

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Views", Assert.Single(result.Failures).PropertyName);
        }

        [Fact]
        public void IsLessThan_ShouldReportFailure_InsteadOfOverflowException_ForOutOfRangeLong()
        {
            // Act
            var result = Validate(new Reading { Views = -3_000_000_000L }, x => x.Views, rule => rule.IsLessThan(0));

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Views", Assert.Single(result.Failures).PropertyName);
        }

        [Fact]
        public void IsGreaterThan_ShouldReportFailure_InsteadOfFormatException_ForNonNumericString()
        {
            // Act
            var result = Validate(new Reading { Code = "abc" }, x => x.Code, rule => rule.IsGreaterThan(0));

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Code", Assert.Single(result.Failures).PropertyName);
        }

        [Fact]
        public void IsLessThan_ShouldReportFailure_InsteadOfFormatException_ForNonNumericString()
        {
            // Act
            var result = Validate(new Reading { Code = "abc" }, x => x.Code, rule => rule.IsLessThan(10));

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Code", Assert.Single(result.Failures).PropertyName);
        }

        [Fact]
        public void IntOverloads_ShouldReportFailure_InsteadOfInvalidCastException_ForDateTime()
        {
            // Act
            var greaterThan = Validate(new Reading { CreatedAt = new DateTime(2026, 1, 1) }, x => x.CreatedAt, rule => rule.IsGreaterThan(0));
            var lessThan = Validate(new Reading { CreatedAt = new DateTime(2026, 1, 1) }, x => x.CreatedAt, rule => rule.IsLessThan(0));

            // Assert
            Assert.False(greaterThan.IsValid);
            Assert.False(lessThan.IsValid);
        }

        [Fact]
        public async Task IsGreaterThan_ShouldNotThrow_FromValidateAsync_ForNonNumericString()
        {
            // Arrange
            var validator = new ReadingValidator();
            validator.RuleFor(x => x.Code).IsGreaterThan(0);

            // Act
            var result = await validator.ValidateAsync(new Reading { Code = "abc" });

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Code", Assert.Single(result.Failures).PropertyName);
        }

        [Theory]
        [InlineData("5", 4, true)]
        [InlineData("5", 5, false)]
        public void IsGreaterThan_ShouldStillConvertNumericString(string code, int minValue, bool expected)
        {
            // Act
            var result = Validate(new Reading { Code = code }, x => x.Code, rule => rule.IsGreaterThan(minValue));

            // Assert
            Assert.Equal(expected, result.IsValid);
        }

        [Fact]
        public void IntOverloads_ShouldStillTreatNullNullableIntAsZero()
        {
            // Act
            var greaterThan = Validate(new Reading { Stock = null }, x => x.Stock, rule => rule.IsGreaterThan(-1));
            var lessThan = Validate(new Reading { Stock = null }, x => x.Stock, rule => rule.IsLessThan(1));

            // Assert
            Assert.True(greaterThan.IsValid);
            Assert.True(lessThan.IsValid);
        }

        [Fact]
        public void IntOverloads_ShouldStillTreatNullStringAsZero()
        {
            // Act
            var greaterThan = Validate(new Reading { Code = null }, x => x.Code, rule => rule.IsGreaterThan(-1));
            var lessThan = Validate(new Reading { Code = null }, x => x.Code, rule => rule.IsLessThan(1));

            // Assert
            Assert.True(greaterThan.IsValid);
            Assert.True(lessThan.IsValid);
        }
    }
}
