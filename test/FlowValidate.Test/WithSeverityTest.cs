using FlowValidate.Enums;
using FlowValidate.Models;

namespace FlowValidate.Test
{
    public class WithSeverityTest
    {
        private class Account
        {
            public string? Name { get; set; }
            public string? BackupEmail { get; set; }
            public string? Nickname { get; set; }
            public List<Account> Related { get; set; } = new();
            public Account? Manager { get; set; }
        }

        private class SingleRuleValidator : BaseValidator<Account>
        {
            public SingleRuleValidator()
            {
                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithSeverity(Severity.Warning);
            }
        }

        [Fact]
        public void WithSeverity_SetsSeverityOnFailure_ForSingleRule()
        {
            var validator = new SingleRuleValidator();

            var result = validator.Validate(new Account { Name = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(Severity.Warning, failure.Severity);
        }

        [Fact]
        public void WithSeverity_MarksResultInvalid_EvenForWarning()
        {
            var validator = new SingleRuleValidator();

            var result = validator.Validate(new Account { Name = "" });

            Assert.False(result.IsValid);
        }

        private class MessageThenSeverityValidator : BaseValidator<Account>
        {
            public MessageThenSeverityValidator()
            {
                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithMessage("Name is required.", "NAME_REQUIRED")
                    .WithSeverity(Severity.Warning);
            }
        }

        private class SeverityThenMessageValidator : BaseValidator<Account>
        {
            public SeverityThenMessageValidator()
            {
                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithSeverity(Severity.Info)
                    .WithMessage("Name is required.", "NAME_REQUIRED");
            }
        }

        [Fact]
        public void WithMessage_ThenWithSeverity_AppliesBoth()
        {
            var validator = new MessageThenSeverityValidator();

            var result = validator.Validate(new Account { Name = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal("Name is required.", failure.ErrorMessage);
            Assert.Equal("NAME_REQUIRED", failure.ErrorCode);
            Assert.Equal(Severity.Warning, failure.Severity);
        }

        [Fact]
        public void WithSeverity_ThenWithMessage_AppliesBoth()
        {
            var validator = new SeverityThenMessageValidator();

            var result = validator.Validate(new Account { Name = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal("Name is required.", failure.ErrorMessage);
            Assert.Equal("NAME_REQUIRED", failure.ErrorCode);
            Assert.Equal(Severity.Info, failure.Severity);
        }

        private class NoRuleValidator : BaseValidator<Account>
        {
            public NoRuleValidator()
            {
                RuleFor(x => x.Name).WithSeverity(Severity.Warning);
            }
        }

        [Fact]
        public void WithSeverity_IsNoOp_WhenNoRuleHasBeenAdded()
        {
            var validator = new NoRuleValidator();

            var result = validator.Validate(new Account { Name = "" });

            Assert.True(result.IsValid);
            Assert.Empty(result.Failures);
        }

        private class AsyncRuleValidator : BaseValidator<Account>
        {
            public AsyncRuleValidator()
            {
                RuleFor(x => x.Name)
                    .MustAsync(name => Task.FromResult(!string.IsNullOrEmpty(name)))
                    .WithSeverity(Severity.Warning);
            }
        }

        [Fact]
        public async Task WithSeverity_AppliesToAsyncRuleFailure()
        {
            var validator = new AsyncRuleValidator();

            var result = await validator.ValidateAsync(new Account { Name = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(Severity.Warning, failure.Severity);
        }

        private class ShouldRuleValidator : BaseValidator<Account>
        {
            public ShouldRuleValidator()
            {
                RuleFor(x => x.Nickname)
                    .Should((nickname, addError) =>
                    {
                        if (string.IsNullOrEmpty(nickname))
                            addError("Nickname is required.");
                    })
                    .WithSeverity(Severity.Warning);
            }
        }

        [Fact]
        public void WithSeverity_HasNoEffect_OnShouldProducedFailures()
        {
            var validator = new ShouldRuleValidator();

            var result = validator.Validate(new Account { Nickname = null });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(Severity.Error, failure.Severity);
        }

        private class RelatedValidator : BaseValidator<Account>
        {
            public RelatedValidator()
            {
                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithSeverity(Severity.Warning);
            }
        }

        private class ManagerValidator : BaseValidator<Account>
        {
            public ManagerValidator()
            {
                RuleFor(x => x.Name)
                    .IsNotEmpty()
                    .WithSeverity(Severity.Info);
            }
        }

        private class ParentValidator : BaseValidator<Account>
        {
            public ParentValidator()
            {
                ValidateCollection(x => x.Related, new RelatedValidator(), item => item);
                ValidateNested(x => x.Manager, new ManagerValidator());
            }
        }

        [Fact]
        public void WithSeverity_AppliesInsideCollectionAndNestedValidators()
        {
            var validator = new ParentValidator();
            var account = new Account
            {
                Manager = new Account { Name = "" },
                Related = { new Account { Name = "" } }
            };

            var result = validator.Validate(account);

            Assert.Equal(2, result.Failures.Count);
            Assert.Contains(result.Failures, f => f.Severity == Severity.Warning);
            Assert.Contains(result.Failures, f => f.Severity == Severity.Info);
        }
    }
}
