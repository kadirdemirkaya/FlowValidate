using FlowValidate.Builders;
using FlowValidate.Enums;

namespace FlowValidate.Test
{
    public class CollectionIndexedPropertyNameTest
    {
        private class Element
        {
            public string Name { get; set; } = string.Empty;
        }

        private class ElementValidator : BaseValidator<Element>
        {
            public ElementValidator()
            {
                RuleFor(x => x.Name).IsNotEmpty().WithMessage("Name is required.", "NAME_REQUIRED");
            }
        }

        private class RootFailureElementValidator : BaseValidator<Element>
        {
            public RootFailureElementValidator()
            {
                _rules.Add(_ => Task.FromResult(ValidationResult.Failure("Element is invalid.")));
            }
        }

        private class Parent
        {
            public List<Element> Elements { get; set; } = new List<Element>();
        }

        private class ParentDefaultValidator : BaseValidator<Parent>
        {
            public ParentDefaultValidator()
            {
                ValidateCollection(x => x.Elements, new ElementValidator(), item => item);
            }
        }

        private class ParentIndexedValidator : BaseValidator<Parent>
        {
            public ParentIndexedValidator()
            {
                ValidateCollection(x => x.Elements, new ElementValidator(), item => item)
                    .WithIndexedPropertyNames("Items");
            }
        }

        private class ParentRootFailureValidator : BaseValidator<Parent>
        {
            public ParentRootFailureValidator()
            {
                ValidateCollection(x => x.Elements, new RootFailureElementValidator(), item => item)
                    .WithIndexedPropertyNames("Items");
            }
        }

        private class Line
        {
            public int Qty { get; set; }
        }

        private class LineValidator : BaseValidator<Line>
        {
            public LineValidator()
            {
                RuleFor(x => x.Qty).IsInRange(1, 100);
            }
        }

        private class Address
        {
            public string Street { get; set; } = string.Empty;
        }

        private class AddressValidator : BaseValidator<Address>
        {
            public AddressValidator()
            {
                RuleFor(x => x.Street).IsNotEmpty().WithMessage("Street is required.", "STREET_REQUIRED");
            }
        }

        private class Order
        {
            public List<Line> Lines { get; set; } = new List<Line>();
            public Address? Address { get; set; }
        }

        private class OrderValidator : BaseValidator<Order>
        {
            public OrderValidator()
            {
                ValidateCollection(x => x.Lines, new LineValidator(), item => item)
                    .WithIndexedPropertyNames("Lines");

                ValidateNested(x => x.Address, new AddressValidator());
            }
        }

        private class Basket
        {
            public List<Order> Orders { get; set; } = new List<Order>();
        }

        private class BasketValidator : BaseValidator<Basket>
        {
            public BasketValidator()
            {
                ValidateCollection(x => x.Orders, new OrderValidator(), item => item)
                    .WithIndexedPropertyNames("Orders");
            }
        }

        private class Shipment
        {
            public Order? Order { get; set; }
        }

        private class ShipmentValidator : BaseValidator<Shipment>
        {
            public ShipmentValidator()
            {
                ValidateNested(x => x.Order, new OrderValidator());
            }
        }

        private static Parent ParentWithTwoInvalidElements()
        {
            return new Parent
            {
                Elements = new List<Element>
                {
                    new Element { Name = "" },
                    new Element { Name = "" }
                }
            };
        }

        private static Order OrderWithInvalidThirdLine()
        {
            return new Order
            {
                Lines = new List<Line>
                {
                    new Line { Qty = 1 },
                    new Line { Qty = 2 },
                    new Line { Qty = 0 }
                }
            };
        }

        [Fact]
        public void Validate_WithoutOptIn_KeepsPropertyNamesAndMessagesUnchanged()
        {
            // Arrange
            var validator = new ParentDefaultValidator();

            // Act
            var result = validator.Validate(ParentWithTwoInvalidElements());

            // Assert
            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("Name", result.Failures[0].PropertyName);
            Assert.Equal("Name", result.Failures[1].PropertyName);
            Assert.Equal("Element 1: Name is required.", result.Failures[0].ErrorMessage);
            Assert.Equal("Element 2: Name is required.", result.Failures[1].ErrorMessage);
        }

        [Fact]
        public void Validate_WithIndexedPropertyNames_ReportsZeroBasedIndexInPropertyName()
        {
            // Arrange
            var validator = new ParentIndexedValidator();

            // Act
            var result = validator.Validate(ParentWithTwoInvalidElements());

            // Assert
            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("Items[0].Name", result.Failures[0].PropertyName);
            Assert.Equal("Items[1].Name", result.Failures[1].PropertyName);
        }

        [Fact]
        public void Validate_WithIndexedPropertyNames_KeepsMessageAndFailureMetadata()
        {
            // Arrange
            var validator = new ParentIndexedValidator();
            var model = new Parent { Elements = new List<Element> { new Element { Name = "" } } };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Element 1: Name is required.", failure.ErrorMessage);
            Assert.Equal("NAME_REQUIRED", failure.ErrorCode);
            Assert.Equal("", failure.AttemptedValue);
            Assert.Equal(Severity.Error, failure.Severity);
        }

        [Fact]
        public void Validate_WithIndexedPropertyNames_WhenElementFailureHasNoProperty_UsesIndexAlone()
        {
            // Arrange
            var validator = new ParentRootFailureValidator();
            var model = new Parent { Elements = new List<Element> { new Element(), new Element() } };

            // Act
            var result = validator.Validate(model);

            // Assert
            Assert.Equal(new[] { "Items[0]", "Items[1]" }, result.Failures.Select(f => f.PropertyName));
        }

        [Fact]
        public void Validate_CollectionInsideCollection_JoinsBothIndexesIntoOnePath()
        {
            // Arrange
            var validator = new BasketValidator();
            var model = new Basket
            {
                Orders = new List<Order>
                {
                    OrderWithInvalidThirdLine()
                }
            };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Orders[0].Lines[2].Qty", failure.PropertyName);
            Assert.StartsWith("Element 1: Element 3: ", failure.ErrorMessage);
        }

        [Fact]
        public void Validate_NestedValidatorInsideCollection_PrefixesTheNestedFailureWithTheElementIndex()
        {
            // Arrange
            var validator = new BasketValidator();
            var model = new Basket
            {
                Orders = new List<Order>
                {
                    new Order(),
                    new Order { Address = new Address { Street = "" } }
                }
            };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Orders[1].Street", failure.PropertyName);
            Assert.Equal("Element 2: Street is required.", failure.ErrorMessage);
        }

        [Fact]
        public void Validate_CollectionInsideNestedValidator_KeepsTheIndexedPathOfTheInnerCollection()
        {
            // Arrange
            var validator = new ShipmentValidator();
            var model = new Shipment { Order = OrderWithInvalidThirdLine() };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Lines[2].Qty", failure.PropertyName);
        }

        [Fact]
        public void WithIndexedPropertyNames_ReturnsTheSameBuilderForChaining()
        {
            // Arrange
            var builder = new ValidationCollectionBuilder<Parent, Element, Element>(
                p => p.Elements,
                new ElementValidator(),
                item => item);

            // Act
            var chained = builder.WithIndexedPropertyNames("Items");

            // Assert
            Assert.Same(builder, chained);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithIndexedPropertyNames_WithEmptyCollectionName_ThrowsArgumentException(string? collectionName)
        {
            // Arrange
            var builder = new ValidationCollectionBuilder<Parent, Element, Element>(
                p => p.Elements,
                new ElementValidator(),
                item => item);

            // Act
            var exception = Assert.Throws<ArgumentException>(() => builder.WithIndexedPropertyNames(collectionName!));

            // Assert
            Assert.Equal("collectionName", exception.ParamName);
        }
    }
}
