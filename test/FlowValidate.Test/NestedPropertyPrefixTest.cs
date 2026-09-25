using FlowValidate.Builders;
using FlowValidate.Enums;
using FlowValidate.Rules;

namespace FlowValidate.Test
{
    public class NestedPropertyPrefixTest
    {
        private class Address
        {
            public string City { get; set; } = string.Empty;
        }

        private class AddressValidator : BaseValidator<Address>
        {
            public AddressValidator()
            {
                RuleFor(x => x.City).IsNotEmpty().WithMessage("City is required.", "CITY_REQUIRED");
            }
        }

        private class RootFailureAddressValidator : BaseValidator<Address>
        {
            public RootFailureAddressValidator()
            {
                _rules.Add(_ => Task.FromResult(ValidationResult.Failure("Address is invalid.")));
            }
        }

        private class Customer
        {
            public Address? Billing { get; set; }
            public Address? Shipping { get; set; }
        }

        private class CustomerDefaultValidator : BaseValidator<Customer>
        {
            public CustomerDefaultValidator()
            {
                ValidateNested(x => x.Billing, new AddressValidator());
            }
        }

        private class CustomerPrefixedValidator : BaseValidator<Customer>
        {
            public CustomerPrefixedValidator()
            {
                ValidateNested(x => x.Billing, new AddressValidator())
                    .WithPropertyPrefix("Billing");

                ValidateNested(x => x.Shipping, new AddressValidator())
                    .WithPropertyPrefix("Shipping");
            }
        }

        private class CustomerRootFailureValidator : BaseValidator<Customer>
        {
            public CustomerRootFailureValidator()
            {
                ValidateNested(x => x.Billing, new RootFailureAddressValidator())
                    .WithPropertyPrefix("Billing");
            }
        }

        private class CustomerRegistryDefaultValidator : BaseValidator<Customer>
        {
            public CustomerRegistryDefaultValidator()
            {
                ValidateRegistryRules(x => x.Billing!, new AddressValidator());
            }
        }

        private class CustomerRegistryPrefixedValidator : BaseValidator<Customer>
        {
            public CustomerRegistryPrefixedValidator()
            {
                ValidateRegistryRules(x => x.Billing!, new AddressValidator())
                    .WithPropertyPrefix("Billing");

                ValidateRegistryRules(x => x.Shipping!, new AddressValidator())
                    .WithPropertyPrefix("Shipping");
            }
        }

        private class Order
        {
            public List<Customer> Customers { get; set; } = new List<Customer>();
        }

        private class OrderValidator : BaseValidator<Order>
        {
            public OrderValidator()
            {
                ValidateCollection(x => x.Customers, new CustomerPrefixedValidator(), item => item)
                    .WithIndexedPropertyNames("Customers");
            }
        }

        [Fact]
        public void Validate_WithoutOptIn_KeepsPropertyNamesAndMessagesUnchanged()
        {
            // Arrange
            var validator = new CustomerDefaultValidator();

            // Act
            var failure = Assert.Single(validator.Validate(new Customer { Billing = new Address { City = "" } }).Failures);

            // Assert
            Assert.Equal("City", failure.PropertyName);
            Assert.Equal("City is required.", failure.ErrorMessage);
        }

        [Fact]
        public void Validate_WithPropertyPrefix_PrefixesThePropertyName()
        {
            // Arrange
            var validator = new CustomerPrefixedValidator();
            var model = new Customer
            {
                Billing = new Address { City = "" },
                Shipping = new Address { City = "" }
            };

            // Act
            var result = validator.Validate(model);

            // Assert
            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("Billing.City", result.Failures[0].PropertyName);
            Assert.Equal("Shipping.City", result.Failures[1].PropertyName);
        }

        [Fact]
        public void Validate_WithPropertyPrefix_KeepsMessageAndFailureMetadata()
        {
            // Arrange
            var validator = new CustomerPrefixedValidator();
            var model = new Customer { Billing = new Address { City = "" } };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("City is required.", failure.ErrorMessage);
            Assert.Equal("CITY_REQUIRED", failure.ErrorCode);
            Assert.Equal("", failure.AttemptedValue);
            Assert.Equal(Severity.Error, failure.Severity);
        }

        [Fact]
        public void Validate_WithPropertyPrefix_WhenFailureHasNoProperty_UsesPrefixAlone()
        {
            // Arrange
            var validator = new CustomerRootFailureValidator();
            var model = new Customer { Billing = new Address() };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Billing", failure.PropertyName);
        }

        [Fact]
        public void Validate_NestedInsideCollection_JoinsIndexAndPrefixIntoOnePath()
        {
            // Arrange
            var validator = new OrderValidator();
            var model = new Order
            {
                Customers = new List<Customer>
                {
                    new Customer { Billing = new Address { City = "" } }
                }
            };

            // Act
            var failure = Assert.Single(validator.Validate(model).Failures);

            // Assert
            Assert.Equal("Customers[0].Billing.City", failure.PropertyName);
        }

        [Fact]
        public void RegistryValidate_WithoutOptIn_KeepsPropertyNamesUnchanged()
        {
            // Arrange
            var validator = new CustomerRegistryDefaultValidator();

            // Act
            var failure = Assert.Single(validator.Validate(new Customer { Billing = new Address { City = "" } }).Failures);

            // Assert
            Assert.Equal("City", failure.PropertyName);
        }

        [Fact]
        public void RegistryValidate_WithPropertyPrefix_PrefixesThePropertyName()
        {
            // Arrange
            var validator = new CustomerRegistryPrefixedValidator();
            var model = new Customer
            {
                Billing = new Address { City = "" },
                Shipping = new Address { City = "" }
            };

            // Act
            var result = validator.Validate(model);

            // Assert
            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("Billing.City", result.Failures[0].PropertyName);
            Assert.Equal("Shipping.City", result.Failures[1].PropertyName);
        }

        [Fact]
        public void WithPropertyPrefix_ReturnsTheSameBuilderForChaining()
        {
            // Arrange
            var builder = new ValidationNestedBuilder<Customer, Address>(x => x.Billing, new AddressValidator());

            // Act
            var chained = builder.WithPropertyPrefix("Billing");

            // Assert
            Assert.Same(builder, chained);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithPropertyPrefix_WithEmptyPrefix_ThrowsArgumentException(string? prefix)
        {
            // Arrange
            var builder = new ValidationNestedBuilder<Customer, Address>(x => x.Billing, new AddressValidator());

            // Act
            var exception = Assert.Throws<ArgumentException>(() => builder.WithPropertyPrefix(prefix!));

            // Assert
            Assert.Equal("prefix", exception.ParamName);
        }

        [Fact]
        public void RegistryWithPropertyPrefix_ReturnsTheSameBuilderForChaining()
        {
            // Arrange
            var builder = new ValidationRegistryRules<Customer, Address>(x => x.Billing!, new AddressValidator());

            // Act
            var chained = builder.WithPropertyPrefix("Billing");

            // Assert
            Assert.Same(builder, chained);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RegistryWithPropertyPrefix_WithEmptyPrefix_ThrowsArgumentException(string? prefix)
        {
            // Arrange
            var builder = new ValidationRegistryRules<Customer, Address>(x => x.Billing!, new AddressValidator());

            // Act
            var exception = Assert.Throws<ArgumentException>(() => builder.WithPropertyPrefix(prefix!));

            // Assert
            Assert.Equal("prefix", exception.ParamName);
        }
    }
}
