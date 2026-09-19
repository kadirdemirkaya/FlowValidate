using FlowValidate.Models;
using System.Linq.Expressions;

namespace FlowValidate.Test
{
    public class PropertyNameTest
    {
        private class Address
        {
            public string City { get; set; } = string.Empty;
        }

        private class PropertyNameModel
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public Address Address { get; set; } = new();
            public List<string> Items { get; set; } = new() { "first" };
            public string[] Tags { get; set; } = { "tag" };
        }

        private class PropertyNameModelValidator : BaseValidator<PropertyNameModel>
        {
        }

        private static ValidationFailure ValidateFailingRule<TProperty>(
            Expression<Func<PropertyNameModel, TProperty>> property,
            string? errorMessage = null)
        {
            var validator = new PropertyNameModelValidator();
            var builder = validator.RuleFor(property).Must(_ => false);

            if (errorMessage != null)
                builder.WithMessage(errorMessage);

            var result = validator.Validate(new PropertyNameModel { Name = "  Kadir  ", Age = 30 });

            Assert.False(result.IsValid);
            return Assert.Single(result.Failures);
        }

        [Fact]
        public void PropertyName_ShouldBeMemberName_ForSimpleMemberAccess()
        {
            // Act
            var failure = ValidateFailingRule(x => x.Name);

            // Assert
            Assert.Equal("Name", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldBeMemberName_ForConvertedValueTypeMember()
        {
            // Act
            var failure = ValidateFailingRule<object>(x => x.Age);

            // Assert
            Assert.Equal("Age", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldBeLastMemberName_ForNestedMemberAccess()
        {
            // Act
            var failure = ValidateFailingRule(x => x.Address.City);

            // Assert
            Assert.Equal("City", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldNotBeNull_ForListIndexer()
        {
            // Act
            var failure = ValidateFailingRule(x => x.Items[0]);

            // Assert
            Assert.NotNull(failure.PropertyName);
            Assert.Equal("x.Items.get_Item(0)", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldNotBeNull_ForArrayIndexer()
        {
            // Act
            var failure = ValidateFailingRule(x => x.Tags[0]);

            // Assert
            Assert.NotNull(failure.PropertyName);
            Assert.Equal("x.Tags[0]", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldNotBeNull_ForMethodCall()
        {
            // Act
            var failure = ValidateFailingRule(x => x.Name.Trim());

            // Assert
            Assert.NotNull(failure.PropertyName);
            Assert.Equal("x.Name.Trim()", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldNotBeNull_ForParameterItself()
        {
            // Act
            var failure = ValidateFailingRule(x => x);

            // Assert
            Assert.NotNull(failure.PropertyName);
            Assert.Equal("x", failure.PropertyName);
        }

        [Fact]
        public void PropertyName_ShouldNotBeNull_ForMethodCall_WhenCustomMessageIsSet()
        {
            // Act
            var failure = ValidateFailingRule(x => x.Name.Trim(), "Trimmed name is invalid");

            // Assert
            Assert.Equal("Trimmed name is invalid", failure.ErrorMessage);
            Assert.Equal("x.Name.Trim()", failure.PropertyName);
        }
    }
}
