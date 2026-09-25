using FlowValidate.Builders;
using System.Linq.Expressions;

namespace FlowValidate.Test
{
    public class DateComparisonRuleTest
    {
        private class Schedule
        {
            public DateTime UtcMoment { get; set; }
            public DateTime LocalMoment { get; set; }
            public DateTime UnspecifiedMoment { get; set; }
            public DateTime? NullableUtcMoment { get; set; }
            public DateTimeOffset OffsetMoment { get; set; }
            public DateTimeOffset? NullableOffsetMoment { get; set; }
            public DateOnly PlainDate { get; set; }
            public DateOnly? NullablePlainDate { get; set; }
            public string? NotADate { get; set; }
        }

        private class ScheduleValidator : BaseValidator<Schedule>
        {
        }

        private static bool IsValid<TProperty>(
            Schedule model,
            Expression<Func<Schedule, TProperty>> property,
            Action<ValidationRuleBuilder<Schedule, TProperty>> configureRule)
        {
            var validator = new ScheduleValidator();
            configureRule(validator.RuleFor(property));
            return validator.Validate(model).IsValid;
        }

        [Theory]
        [InlineData(-1, false, true)]
        [InlineData(1, true, false)]
        public void UtcDateTime_ShouldCompareAgainstUtcNow_RegardlessOfLocalOffset(int daysFromNow, bool expectFuture, bool expectPast)
        {
            // Arrange
            var model = new Schedule { UtcMoment = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(daysFromNow), DateTimeKind.Utc) };

            // Act & Assert
            Assert.Equal(expectFuture, IsValid(model, x => x.UtcMoment, rule => rule.IsDateInFuture()));
            Assert.Equal(expectFuture, IsValid(model, x => x.UtcMoment, rule => rule.IsInFuture()));
            Assert.Equal(expectPast, IsValid(model, x => x.UtcMoment, rule => rule.IsDateInPast()));
        }

        [Theory]
        [InlineData(-1, false, true)]
        [InlineData(1, true, false)]
        public void LocalDateTime_ShouldCompareAgainstLocalNow(int daysFromNow, bool expectFuture, bool expectPast)
        {
            // Arrange
            var model = new Schedule { LocalMoment = DateTime.SpecifyKind(DateTime.Now.AddDays(daysFromNow), DateTimeKind.Local) };

            // Act & Assert
            Assert.Equal(expectFuture, IsValid(model, x => x.LocalMoment, rule => rule.IsDateInFuture()));
            Assert.Equal(expectPast, IsValid(model, x => x.LocalMoment, rule => rule.IsDateInPast()));
        }

        [Theory]
        [InlineData(-1, false, true)]
        [InlineData(1, true, false)]
        public void UnspecifiedDateTime_ShouldCompareAgainstLocalNow(int daysFromNow, bool expectFuture, bool expectPast)
        {
            // Arrange
            var model = new Schedule { UnspecifiedMoment = DateTime.SpecifyKind(DateTime.Now.AddDays(daysFromNow), DateTimeKind.Unspecified) };

            // Act & Assert
            Assert.Equal(expectFuture, IsValid(model, x => x.UnspecifiedMoment, rule => rule.IsDateInFuture()));
            Assert.Equal(expectPast, IsValid(model, x => x.UnspecifiedMoment, rule => rule.IsDateInPast()));
        }

        [Fact]
        public void NullableUtcDateTime_ShouldFailOnNull_AndCompareAgainstUtcNowOtherwise()
        {
            // Arrange
            var future = new Schedule { NullableUtcMoment = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Utc) };
            var nullValue = new Schedule { NullableUtcMoment = null };

            // Act & Assert
            Assert.True(IsValid(future, x => x.NullableUtcMoment, rule => rule.IsDateInFuture()));
            Assert.False(IsValid(nullValue, x => x.NullableUtcMoment, rule => rule.IsDateInFuture()));
        }

        [Theory]
        [InlineData(-1, false, true)]
        [InlineData(1, true, false)]
        public void DateTimeOffset_ShouldCompareAgainstUtcNow_RegardlessOfItsOwnOffset(int daysFromNow, bool expectFuture, bool expectPast)
        {
            // Arrange
            var model = new Schedule { OffsetMoment = new DateTimeOffset(DateTime.UtcNow.AddDays(daysFromNow), TimeSpan.Zero).ToOffset(TimeSpan.FromHours(5)) };

            // Act & Assert
            Assert.Equal(expectFuture, IsValid(model, x => x.OffsetMoment, rule => rule.IsDateInFuture()));
            Assert.Equal(expectFuture, IsValid(model, x => x.OffsetMoment, rule => rule.IsInFuture()));
            Assert.Equal(expectPast, IsValid(model, x => x.OffsetMoment, rule => rule.IsDateInPast()));
        }

        [Fact]
        public void NullableDateTimeOffset_ShouldFailOnNull_AndCompareOtherwise()
        {
            // Arrange
            var future = new Schedule { NullableOffsetMoment = DateTimeOffset.UtcNow.AddDays(1) };
            var nullValue = new Schedule { NullableOffsetMoment = null };

            // Act & Assert
            Assert.True(IsValid(future, x => x.NullableOffsetMoment, rule => rule.IsDateInFuture()));
            Assert.False(IsValid(nullValue, x => x.NullableOffsetMoment, rule => rule.IsDateInFuture()));
        }

        [Theory]
        [InlineData(-1, false, true)]
        [InlineData(1, true, false)]
        public void DateOnly_ShouldCompareAgainstTodaysLocalDate(int daysFromNow, bool expectFuture, bool expectPast)
        {
            // Arrange
            var model = new Schedule { PlainDate = DateOnly.FromDateTime(DateTime.Now).AddDays(daysFromNow) };

            // Act & Assert
            Assert.Equal(expectFuture, IsValid(model, x => x.PlainDate, rule => rule.IsDateInFuture()));
            Assert.Equal(expectPast, IsValid(model, x => x.PlainDate, rule => rule.IsDateInPast()));
        }

        [Fact]
        public void NullableDateOnly_ShouldFailOnNull_AndCompareOtherwise()
        {
            // Arrange
            var future = new Schedule { NullablePlainDate = DateOnly.FromDateTime(DateTime.Now).AddDays(1) };
            var nullValue = new Schedule { NullablePlainDate = null };

            // Act & Assert
            Assert.True(IsValid(future, x => x.NullablePlainDate, rule => rule.IsDateInFuture()));
            Assert.False(IsValid(nullValue, x => x.NullablePlainDate, rule => rule.IsDateInFuture()));
        }

        [Fact]
        public void NonDateValue_ShouldAlwaysFail()
        {
            // Arrange
            var model = new Schedule { NotADate = "not a date" };

            // Act & Assert
            Assert.False(IsValid(model, x => x.NotADate, rule => rule.IsDateInFuture()));
            Assert.False(IsValid(model, x => x.NotADate, rule => rule.IsDateInPast()));
        }
    }
}
