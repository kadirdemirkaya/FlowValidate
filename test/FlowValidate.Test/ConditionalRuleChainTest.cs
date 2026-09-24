namespace FlowValidate.Test
{
    public class ConditionalRuleChainTest
    {
        private class Customer
        {
            public bool IsCompany { get; set; }
            public bool IsDraft { get; set; }
            public string? VatNumber { get; set; }
            public string? Name { get; set; }
            public Address? Address { get; set; }
            public List<Contact> Contacts { get; set; } = new List<Contact>();
        }

        private class Address
        {
            public string? City { get; set; }
            public string? Country { get; set; }
        }

        private class Contact
        {
            public bool NotifyByEmail { get; set; }
            public string? Email { get; set; }
        }

        private class CompanyVatValidator : BaseValidator<Customer>
        {
            public CompanyVatValidator()
            {
                RuleFor(x => x.VatNumber)
                    .IsNotEmpty()
                    .WithMessage("VAT number is required for companies.", "VAT_REQUIRED")
                    .When(x => x.IsCompany);
            }
        }

        private class DraftUnlessValidator : BaseValidator<Customer>
        {
            public DraftUnlessValidator()
            {
                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithMessage("Name is required.", "NAME_REQUIRED")
                    .Unless(x => x.IsDraft);
            }
        }

        private class ConditionPlacementValidator : BaseValidator<Customer>
        {
            public ConditionPlacementValidator()
            {
                RuleFor(x => x.VatNumber)
                    .When(x => x.IsCompany)
                    .IsNotEmpty()
                    .WithMessage("VAT number is required for companies.", "VAT_REQUIRED");
            }
        }

        private class CombinedConditionsValidator : BaseValidator<Customer>
        {
            public CombinedConditionsValidator()
            {
                RuleFor(x => x.VatNumber)
                    .IsNotEmpty()
                    .WithMessage("VAT number is required for companies.", "VAT_REQUIRED")
                    .When(x => x.IsCompany)
                    .Unless(x => x.IsDraft);
            }
        }

        private class UnreadPropertyValidator : BaseValidator<Customer>
        {
            public UnreadPropertyValidator()
            {
                RuleFor(x => x.Address!.City)
                    .IsNotEmpty()
                    .WithMessage("City is required.", "CITY_REQUIRED")
                    .When(x => x.Address != null);
            }
        }

        private class OtherChainUntouchedValidator : BaseValidator<Customer>
        {
            public OtherChainUntouchedValidator()
            {
                RuleFor(x => x.VatNumber)
                    .IsNotEmpty()
                    .WithMessage("VAT number is required for companies.", "VAT_REQUIRED")
                    .When(x => x.IsCompany);

                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithMessage("Name is required.", "NAME_REQUIRED");
            }
        }

        private class AsyncConditionalValidator : BaseValidator<Customer>
        {
            public static int MustAsyncCalls;
            public static int ShouldAsyncCalls;

            public AsyncConditionalValidator()
            {
                RuleFor(x => x.VatNumber)
                    .MustAsync(async value =>
                    {
                        MustAsyncCalls++;
                        await Task.Yield();
                        return !string.IsNullOrWhiteSpace(value);
                    })
                    .WithMessage("VAT number is required for companies.", "VAT_REQUIRED")
                    .ShouldAsync(async value =>
                    {
                        ShouldAsyncCalls++;
                        await Task.Yield();
                        throw new InvalidOperationException();
                    }, "VAT number could not be verified.")
                    .When(x => x.IsCompany);
            }
        }

        private class RequiredIfInsideWhenValidator : BaseValidator<Customer>
        {
            public RequiredIfInsideWhenValidator()
            {
                RuleFor(x => x.VatNumber)
                    .RequiredIf(value => value is not null && value.StartsWith("VAT"))
                    .Length(8, 12)
                    .When(x => x.IsCompany);
            }
        }

        private class AddressValidator : BaseValidator<Address>
        {
            public AddressValidator()
            {
                RuleFor(x => x.City)
                    .IsNotEmpty()
                    .WithMessage("City is required.", "CITY_REQUIRED")
                    .When(x => x.Country == "TR");
            }
        }

        private class NestedConditionalValidator : BaseValidator<Customer>
        {
            public NestedConditionalValidator()
            {
                ValidateNested(x => x.Address, new AddressValidator());
            }
        }

        private class ContactValidator : BaseValidator<Contact>
        {
            public ContactValidator()
            {
                RuleFor(x => x.Email)
                    .IsEmail()
                    .WithMessage("A valid email is required.", "EMAIL_INVALID")
                    .When(x => x.NotifyByEmail);
            }
        }

        private class CollectionConditionalValidator : BaseValidator<Customer>
        {
            public CollectionConditionalValidator()
            {
                ValidateCollection(x => x.Contacts, new ContactValidator(), item => item);
            }
        }

        [Fact]
        public void When_Condition_True_Runs_The_Chain()
        {
            // Arrange
            var validator = new CompanyVatValidator();
            var customer = new Customer { IsCompany = true, VatNumber = null };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("VatNumber", failure.PropertyName);
            Assert.Equal("VAT number is required for companies.", failure.ErrorMessage);
            Assert.Equal("VAT_REQUIRED", failure.ErrorCode);
        }

        [Fact]
        public void When_Condition_False_Skips_The_Chain_Without_Failures()
        {
            // Arrange
            var validator = new CompanyVatValidator();
            var customer = new Customer { IsCompany = false, VatNumber = null };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
        }

        [Fact]
        public void Unless_Condition_True_Skips_The_Chain_Without_Failures()
        {
            // Arrange
            var validator = new DraftUnlessValidator();
            var customer = new Customer { IsDraft = true, Name = "   " };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
        }

        [Fact]
        public void Unless_Condition_False_Runs_The_Chain()
        {
            // Arrange
            var validator = new DraftUnlessValidator();
            var customer = new Customer { IsDraft = false, Name = "   " };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Name", failure.PropertyName);
            Assert.Equal("NAME_REQUIRED", failure.ErrorCode);
        }

        [Fact]
        public void When_Reads_Another_Property_Of_The_Root_Instance()
        {
            // Arrange
            var validator = new CompanyVatValidator();
            var company = new Customer { IsCompany = true, VatNumber = "VAT-123" };
            var person = new Customer { IsCompany = false, VatNumber = "VAT-123" };

            // Act
            var companyResult = validator.Validate(company);
            var personResult = validator.Validate(person);

            // Assert
            Assert.True(companyResult.IsValid);
            Assert.True(personResult.IsValid);
            Assert.False(validator.Validate(new Customer { IsCompany = true }).IsValid);
            Assert.True(validator.Validate(new Customer { IsCompany = false }).IsValid);
        }

        [Fact]
        public void When_Gates_The_Whole_Chain_Regardless_Of_Placement()
        {
            // Arrange
            var validator = new ConditionPlacementValidator();

            // Act
            var skipped = validator.Validate(new Customer { IsCompany = false, VatNumber = null });
            var applied = validator.Validate(new Customer { IsCompany = true, VatNumber = null });

            // Assert
            Assert.True(skipped.IsValid);
            Assert.Empty(skipped.Failures);
            Assert.False(applied.IsValid);
            Assert.Equal("VAT_REQUIRED", Assert.Single(applied.Failures).ErrorCode);
        }

        [Fact]
        public void Multiple_Conditions_Are_Combined_With_And()
        {
            // Arrange
            var validator = new CombinedConditionsValidator();

            // Act
            var companyDraft = validator.Validate(new Customer { IsCompany = true, IsDraft = true });
            var personFinal = validator.Validate(new Customer { IsCompany = false, IsDraft = false });
            var companyFinal = validator.Validate(new Customer { IsCompany = true, IsDraft = false });

            // Assert
            Assert.True(companyDraft.IsValid);
            Assert.True(personFinal.IsValid);
            Assert.False(companyFinal.IsValid);
            Assert.Equal("VAT_REQUIRED", Assert.Single(companyFinal.Failures).ErrorCode);
        }

        [Fact]
        public void Skipped_Chain_Does_Not_Read_The_Property()
        {
            // Arrange
            var validator = new UnreadPropertyValidator();
            var customer = new Customer { Address = null };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
            Assert.False(validator.Validate(new Customer { Address = new Address { City = null } }).IsValid);
        }

        [Fact]
        public void Skipped_Chain_Does_Not_Affect_Other_Properties()
        {
            // Arrange
            var validator = new OtherChainUntouchedValidator();
            var customer = new Customer { IsCompany = false, VatNumber = null, Name = null };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Name", failure.PropertyName);
            Assert.Equal("NAME_REQUIRED", failure.ErrorCode);
        }

        [Fact]
        public async Task When_Condition_False_Skips_Async_Rules()
        {
            // Arrange
            AsyncConditionalValidator.MustAsyncCalls = 0;
            AsyncConditionalValidator.ShouldAsyncCalls = 0;
            var validator = new AsyncConditionalValidator();

            // Act
            var result = await validator.ValidateAsync(new Customer { IsCompany = false, VatNumber = null });

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
            Assert.Equal(0, AsyncConditionalValidator.MustAsyncCalls);
            Assert.Equal(0, AsyncConditionalValidator.ShouldAsyncCalls);
            Assert.True(validator.HasAsyncRules);
        }

        [Fact]
        public async Task When_Condition_True_Runs_Async_Rules()
        {
            // Arrange
            AsyncConditionalValidator.MustAsyncCalls = 0;
            AsyncConditionalValidator.ShouldAsyncCalls = 0;
            var validator = new AsyncConditionalValidator();

            // Act
            var result = await validator.ValidateAsync(new Customer { IsCompany = true, VatNumber = null });

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(1, AsyncConditionalValidator.MustAsyncCalls);
            Assert.Equal(1, AsyncConditionalValidator.ShouldAsyncCalls);
            Assert.Contains(result.Failures, f => f.ErrorCode == "VAT_REQUIRED");
            Assert.Contains(result.Failures, f => f.ErrorMessage == "VAT number could not be verified.");
        }

        [Fact]
        public void Skipped_Async_Chain_Still_Makes_Validate_Throw()
        {
            // Arrange
            var validator = new AsyncConditionalValidator();

            // Act
            var exception = Record.Exception(() => validator.Validate(new Customer { IsCompany = false }));

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
        }

        [Fact]
        public void When_Does_Not_Change_RequiredIf_Behaviour_Inside_The_Chain()
        {
            // Arrange
            var validator = new RequiredIfInsideWhenValidator();

            // Act
            var skipped = validator.Validate(new Customer { IsCompany = false, VatNumber = "ABC" });
            var requiredIfFailed = validator.Validate(new Customer { IsCompany = true, VatNumber = "ABC" });
            var requiredIfPassed = validator.Validate(new Customer { IsCompany = true, VatNumber = "VAT-1" });

            // Assert
            Assert.True(skipped.IsValid);
            Assert.Empty(skipped.Failures);

            var requiredFailure = Assert.Single(requiredIfFailed.Failures);
            Assert.Equal("Property is required.", requiredFailure.ErrorMessage);
            Assert.Equal("Required", requiredFailure.ErrorCode);

            var lengthFailure = Assert.Single(requiredIfPassed.Failures);
            Assert.Equal("Validation failed for property.", lengthFailure.ErrorMessage);
            Assert.Equal("DefaultRule", lengthFailure.ErrorCode);
        }

        [Fact]
        public void When_Works_Inside_A_Nested_Validator()
        {
            // Arrange
            var validator = new NestedConditionalValidator();

            // Act
            var skipped = validator.Validate(new Customer { Address = new Address { Country = "DE", City = null } });
            var applied = validator.Validate(new Customer { Address = new Address { Country = "TR", City = null } });

            // Assert
            Assert.True(skipped.IsValid);
            Assert.Empty(skipped.Failures);
            Assert.False(applied.IsValid);
            Assert.Equal("CITY_REQUIRED", Assert.Single(applied.Failures).ErrorCode);
        }

        [Fact]
        public void When_Works_Inside_A_Collection_Element_Validator()
        {
            // Arrange
            var validator = new CollectionConditionalValidator();
            var customer = new Customer
            {
                Contacts = new List<Contact>
                {
                    new Contact { NotifyByEmail = false, Email = "not-an-email" },
                    new Contact { NotifyByEmail = true, Email = "not-an-email" }
                }
            };

            // Act
            var result = validator.Validate(customer);

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Element 2: A valid email is required.", failure.ErrorMessage);
            Assert.Equal("EMAIL_INVALID", failure.ErrorCode);
        }

        [Fact]
        public void When_And_Unless_Reject_A_Null_Condition()
        {
            // Arrange
            var builder = new FlowValidate.Builders.ValidationRuleBuilder<Customer, string?>(x => x.Name);

            // Act
            var whenException = Record.Exception(() => builder.When(null!));
            var unlessException = Record.Exception(() => builder.Unless(null!));

            // Assert
            Assert.IsType<ArgumentNullException>(whenException);
            Assert.IsType<ArgumentNullException>(unlessException);
        }
    }
}
