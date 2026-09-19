using FlowValidate.Builders;
using System.Globalization;
using System.Linq.Expressions;

namespace FlowValidate.Test
{
    public class ComparableRuleTest
    {
        private static readonly DateTime RangeStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime RangeEnd = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        private class Measurement
        {
            public int Quantity { get; set; }
            public int? Stock { get; set; }
            public decimal Price { get; set; }
            public double Ratio { get; set; }
            public long Views { get; set; }
            public DateTime CreatedAt { get; set; }
            public decimal? Discount { get; set; }
            public double? Score { get; set; }
            public long? Downloads { get; set; }
            public DateTime? ShippedAt { get; set; }
            public string? Code { get; set; }
        }

        private class MeasurementValidator : BaseValidator<Measurement>
        {
        }

        private static bool IsValid<TProperty>(
            Measurement model,
            Expression<Func<Measurement, TProperty>> property,
            Action<ValidationRuleBuilder<Measurement, TProperty>> configureRule)
        {
            var validator = new MeasurementValidator();
            configureRule(validator.RuleFor(property));
            return validator.Validate(model).IsValid;
        }

        private static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

        [Fact]
        public void IntBounds_ShouldStillBindToIntOverloads()
        {
            // Arrange
            var validator = new MeasurementValidator();
            var intBuilder = validator.RuleFor(x => x.Quantity);
            var decimalBuilder = validator.RuleFor(x => x.Price);

            // Act
            Func<int, int, ValidationRuleBuilder<Measurement, int>> intRange = intBuilder.IsInRange;
            Func<int, ValidationRuleBuilder<Measurement, int>> intGreater = intBuilder.IsGreaterThan;
            Func<int, ValidationRuleBuilder<Measurement, int>> intLess = intBuilder.IsLessThan;
            Func<int, int, ValidationRuleBuilder<Measurement, decimal>> decimalRange = decimalBuilder.IsInRange;
            Func<int, ValidationRuleBuilder<Measurement, decimal>> decimalGreater = decimalBuilder.IsGreaterThan;
            Func<int, ValidationRuleBuilder<Measurement, decimal>> decimalLess = decimalBuilder.IsLessThan;

            // Assert
            Assert.Equal(typeof(ValidationRuleBuilder<Measurement, int>), intRange.Method.DeclaringType);
            Assert.Equal(typeof(ValidationRuleBuilder<Measurement, int>), intGreater.Method.DeclaringType);
            Assert.Equal(typeof(ValidationRuleBuilder<Measurement, int>), intLess.Method.DeclaringType);
            Assert.Equal(typeof(ValidationRuleBuilder<Measurement, decimal>), decimalRange.Method.DeclaringType);
            Assert.Equal(typeof(ValidationRuleBuilder<Measurement, decimal>), decimalGreater.Method.DeclaringType);
            Assert.Equal(typeof(ValidationRuleBuilder<Measurement, decimal>), decimalLess.Method.DeclaringType);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(10, true)]
        [InlineData(11, false)]
        public void IntBounds_IsInRange_ShouldKeepIntBehavior_ForIntProperty(int quantity, bool expected)
        {
            // Act
            var isValid = IsValid(new Measurement { Quantity = quantity }, x => x.Quantity, rule => rule.IsInRange(1, 10));

            // Assert
            Assert.Equal(expected, isValid);
        }

        [Fact]
        public void IntBounds_IsInRange_ShouldStillRejectNonIntValue()
        {
            // Act
            var isValid = IsValid(new Measurement { Price = 5m }, x => x.Price, rule => rule.IsInRange(1, 10));

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void IntBounds_IsGreaterThan_ShouldStillTruncateDecimalValue()
        {
            // Act
            var isValid = IsValid(new Measurement { Price = 5.4m }, x => x.Price, rule => rule.IsGreaterThan(5));

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void IntBounds_ShouldStillTreatNullNullableIntAsZero()
        {
            // Act
            var greaterThanNegative = IsValid(new Measurement { Stock = null }, x => x.Stock, rule => rule.IsGreaterThan(-1));
            var lessThanOne = IsValid(new Measurement { Stock = null }, x => x.Stock, rule => rule.IsLessThan(1));

            // Assert
            Assert.True(greaterThanNegative);
            Assert.True(lessThanOne);
        }

        [Theory]
        [InlineData("9.99", false, false, true)]
        [InlineData("10", true, false, true)]
        [InlineData("15.5", true, true, true)]
        [InlineData("20", true, true, false)]
        [InlineData("20.01", false, true, false)]
        public void DecimalBounds_ShouldCompareWithoutTruncation(string price, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Price = ParseDecimal(price) };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Price, rule => rule.IsInRange(10m, 20m)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Price, rule => rule.IsGreaterThan(10m)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Price, rule => rule.IsLessThan(20m)));
        }

        [Theory]
        [InlineData(0.49, false, false, true)]
        [InlineData(0.5, true, false, true)]
        [InlineData(0.75, true, true, true)]
        [InlineData(1.0, true, true, false)]
        [InlineData(1.01, false, true, false)]
        public void DoubleBounds_ShouldCompareFractionalValues(double ratio, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Ratio = ratio };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Ratio, rule => rule.IsInRange(0.5, 1.0)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Ratio, rule => rule.IsGreaterThan(0.5)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Ratio, rule => rule.IsLessThan(1.0)));
        }

        [Theory]
        [InlineData(2_999_999_999L, false, false, true)]
        [InlineData(3_000_000_000L, true, false, true)]
        [InlineData(3_500_000_000L, true, true, true)]
        [InlineData(4_000_000_000L, true, true, false)]
        [InlineData(4_000_000_001L, false, true, false)]
        public void LongBounds_ShouldCompareValuesBeyondIntRange(long views, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Views = views };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Views, rule => rule.IsInRange(3_000_000_000L, 4_000_000_000L)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Views, rule => rule.IsGreaterThan(3_000_000_000L)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Views, rule => rule.IsLessThan(4_000_000_000L)));
        }

        [Theory]
        [InlineData(-1, false, false, true)]
        [InlineData(0, true, false, true)]
        [InlineData(100, true, true, true)]
        [InlineData(364, true, true, false)]
        [InlineData(365, false, true, false)]
        public void DateTimeBounds_ShouldCompareDates(int daysAfterStart, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { CreatedAt = RangeStart.AddDays(daysAfterStart) };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.CreatedAt, rule => rule.IsInRange(RangeStart, RangeEnd)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.CreatedAt, rule => rule.IsGreaterThan(RangeStart)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.CreatedAt, rule => rule.IsLessThan(RangeEnd)));
        }

        [Theory]
        [InlineData("9.99", false, false, true)]
        [InlineData("15.5", true, true, true)]
        [InlineData("20.01", false, true, false)]
        [InlineData(null, false, false, false)]
        public void NullableDecimalBounds_ShouldCompareValue_AndFailOnNull(string? discount, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Discount = discount == null ? null : ParseDecimal(discount) };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Discount, rule => rule.IsInRange(10m, 20m)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Discount, rule => rule.IsGreaterThan(10m)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Discount, rule => rule.IsLessThan(20m)));
        }

        [Theory]
        [InlineData(0.49, false, false, true)]
        [InlineData(0.75, true, true, true)]
        [InlineData(1.01, false, true, false)]
        [InlineData(null, false, false, false)]
        public void NullableDoubleBounds_ShouldCompareValue_AndFailOnNull(double? score, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Score = score };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Score, rule => rule.IsInRange(0.5, 1.0)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Score, rule => rule.IsGreaterThan(0.5)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Score, rule => rule.IsLessThan(1.0)));
        }

        [Theory]
        [InlineData(2_999_999_999L, false, false, true)]
        [InlineData(3_500_000_000L, true, true, true)]
        [InlineData(4_000_000_001L, false, true, false)]
        [InlineData(null, false, false, false)]
        public void NullableLongBounds_ShouldCompareValue_AndFailOnNull(long? downloads, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Downloads = downloads };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Downloads, rule => rule.IsInRange(3_000_000_000L, 4_000_000_000L)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Downloads, rule => rule.IsGreaterThan(3_000_000_000L)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Downloads, rule => rule.IsLessThan(4_000_000_000L)));
        }

        [Theory]
        [InlineData(-1, false, false, true)]
        [InlineData(100, true, true, true)]
        [InlineData(365, false, true, false)]
        [InlineData(null, false, false, false)]
        public void NullableDateTimeBounds_ShouldCompareValue_AndFailOnNull(int? daysAfterStart, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { ShippedAt = daysAfterStart == null ? null : RangeStart.AddDays(daysAfterStart.Value) };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.ShippedAt, rule => rule.IsInRange(RangeStart, RangeEnd)));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.ShippedAt, rule => rule.IsGreaterThan(RangeStart)));
            Assert.Equal(lessThanMax, IsValid(model, x => x.ShippedAt, rule => rule.IsLessThan(RangeEnd)));
        }

        [Theory]
        [InlineData("a", false, false, true)]
        [InlineData("c", true, true, true)]
        [InlineData("e", false, true, false)]
        [InlineData(null, false, false, false)]
        public void ReferenceTypeBounds_ShouldCompareValue_AndFailOnNull(string? code, bool inRange, bool greaterThanMin, bool lessThanMax)
        {
            // Arrange
            var model = new Measurement { Code = code };

            // Act & Assert
            Assert.Equal(inRange, IsValid(model, x => x.Code, rule => rule.IsInRange("b", "d")));
            Assert.Equal(greaterThanMin, IsValid(model, x => x.Code, rule => rule.IsGreaterThan("b")));
            Assert.Equal(lessThanMax, IsValid(model, x => x.Code, rule => rule.IsLessThan("d")));
        }

        [Fact]
        public void ComparableRules_ShouldReturnSameBuilder_AndCarryCustomMessage()
        {
            // Arrange
            var validator = new MeasurementValidator();
            var builder = validator.RuleFor(x => x.Price);
            var nullableBuilder = validator.RuleFor(x => x.Discount);

            // Act
            var chained = builder.IsInRange(1m, 100m).IsGreaterThan(10m).IsLessThan(50m).WithMessage("Price must be between 10 and 50.", "PRICE_RANGE");
            var nullableChained = nullableBuilder.IsInRange(0m, 1m).IsGreaterThan(0m).IsLessThan(1m);
            var result = validator.Validate(new Measurement { Price = 60m, Discount = 0.5m });

            // Assert
            Assert.Same(builder, chained);
            Assert.Same(nullableBuilder, nullableChained);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Price", failure.PropertyName);
            Assert.Equal("Price must be between 10 and 50.", failure.ErrorMessage);
            Assert.Equal("PRICE_RANGE", failure.ErrorCode);
        }
    }
}
