using FlowValidate.Rules;

namespace FlowValidate.Test
{
    public class NullCompositionTest
    {
        private class Child
        {
            public string Name { get; set; } = string.Empty;
        }

        private class ChildValidator : BaseValidator<Child>
        {
            public ChildValidator()
            {
                RuleFor(x => x.Name).IsNotEmpty().WithMessage("Name is required.", "NAME_REQUIRED");
            }
        }

        private class Parent
        {
            public Child? Child { get; set; }
            public List<Child?> Kids { get; set; } = new();
        }

        private class ParentRegistryValidator : BaseValidator<Parent>
        {
            public ParentRegistryValidator()
            {
                ValidateRegistryRules(p => p.Child!, new ChildValidator());
            }
        }

        private class ParentCollectionValidator : BaseValidator<Parent>
        {
            public ParentCollectionValidator()
            {
                ValidateCollection(p => p.Kids, new ChildValidator(), item => item!);
            }
        }

        private class ParentIndexedCollectionValidator : BaseValidator<Parent>
        {
            public ParentIndexedCollectionValidator()
            {
                ValidateCollection(p => p.Kids, new ChildValidator(), item => item!)
                    .WithIndexedPropertyNames("Kids");
            }
        }

        private class Basket
        {
            public List<Parent> Parents { get; set; } = new();
        }

        private class BasketValidator : BaseValidator<Basket>
        {
            public BasketValidator()
            {
                ValidateCollection(b => b.Parents, new ParentCollectionValidator(), item => item)
                    .WithIndexedPropertyNames("Parents");
            }
        }

        [Fact]
        public void ValidateRegistryRules_WithNullValue_SkipsTheStepWithoutThrowing()
        {
            // Arrange
            var validator = new ParentRegistryValidator();
            var model = new Parent { Child = null };

            // Act
            var result = validator.Validate(model);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
        }

        [Fact]
        public void ValidateRegistryRules_WithNonNullInvalidValue_StillReportsFailures()
        {
            // Arrange
            var validator = new ParentRegistryValidator();
            var model = new Parent { Child = new Child { Name = "" } };

            // Act
            var result = validator.Validate(model);

            // Assert
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Name is required.", failure.ErrorMessage);
        }

        [Fact]
        public void ValidateCollection_WithNullElement_ReportsOneNullElementFailureWithoutThrowing()
        {
            // Arrange
            var validator = new ParentCollectionValidator();
            var model = new Parent { Kids = new List<Child?> { null } };

            // Act
            var result = validator.Validate(model);

            // Assert
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Element 1: cannot be null.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.NullElement, failure.ErrorCode);
            Assert.Equal("<root>", failure.PropertyName);
        }

        [Fact]
        public void ValidateCollection_WithNullElement_AndIndexedNamesOn_ReportsIndexOnlyPropertyName()
        {
            // Arrange
            var validator = new ParentIndexedCollectionValidator();
            var model = new Parent { Kids = new List<Child?> { null } };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Kids[0]", failure.PropertyName);
            Assert.Equal("Element 1: cannot be null.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.NullElement, failure.ErrorCode);
        }

        [Fact]
        public void ValidateCollection_WithMixedValidNullAndInvalidElements_ValidatesEachIndependently()
        {
            // Arrange
            var validator = new ParentIndexedCollectionValidator();
            var model = new Parent
            {
                Kids = new List<Child?>
                {
                    new Child { Name = "Ada" },
                    null,
                    new Child { Name = "" }
                }
            };

            // Act
            var result = validator.Validate(model);

            // Assert
            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("Kids[1]", result.Failures[0].PropertyName);
            Assert.Equal(BuiltInRuleCodes.NullElement, result.Failures[0].ErrorCode);
            Assert.Equal("Kids[2].Name", result.Failures[1].PropertyName);
            Assert.Equal("NAME_REQUIRED", result.Failures[1].ErrorCode);
        }

        [Fact]
        public void ValidateCollection_NestedInsideAnotherCollection_ReportsNullElementWithCombinedIndex()
        {
            // Arrange
            var validator = new BasketValidator();
            var model = new Basket
            {
                Parents = new List<Parent>
                {
                    new Parent { Kids = new List<Child?> { null } }
                }
            };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Parents[0]", failure.PropertyName);
            Assert.Equal("Element 1: Element 1: cannot be null.", failure.ErrorMessage);
            Assert.Equal(BuiltInRuleCodes.NullElement, failure.ErrorCode);
        }
    }
}
