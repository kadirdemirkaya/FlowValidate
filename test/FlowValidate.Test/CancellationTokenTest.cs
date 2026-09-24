using FlowValidate.Abstractions;
using FlowValidate.Builders;
using FlowValidate.Rules;
using System.Linq.Expressions;

namespace FlowValidate.Test
{
    public class CancellationTokenTest
    {
        private class Account
        {
            public string? Email { get; set; }
            public string? Nickname { get; set; }
            public Profile? Profile { get; set; }
            public List<Profile> Visited { get; set; } = new();
        }

        private class Profile
        {
            public string? City { get; set; }
        }

        private class AccountValidator : BaseValidator<Account>
        {
        }

        private class ProfileValidator : BaseValidator<Profile>
        {
            public CancellationToken ObservedToken { get; private set; }

            public ProfileValidator()
            {
                RuleFor(x => x.City).MustAsync((city, token) =>
                {
                    ObservedToken = token;

                    return Task.FromResult(!string.IsNullOrEmpty(city));
                });
            }
        }

        private class HandWrittenValidator : IBaseValidator<Account>
        {
            public int ValidateAsyncCalls { get; private set; }

            public Task<ValidationResult> ValidateAsync(Account instance)
            {
                ValidateAsyncCalls++;

                return Task.FromResult(ValidationResult.Failure("Hand written failure.", "Email"));
            }

            public ValidationResult Validate(Account instance) => ValidateAsync(instance).GetAwaiter().GetResult();

            public ValidationRuleBuilder<Account, TProperty> RuleFor<TProperty>(Expression<Func<Account, TProperty>> property)
                => throw new NotSupportedException();

            public ValidationNestedBuilder<Account, TProperty> ValidateNested<TProperty>(Func<Account, TProperty?> propertyFunc, BaseValidator<TProperty> validator)
                => throw new NotSupportedException();

            public ValidationCollectionBuilder<Account, TCollection, TElement> ValidateCollection<TCollection, TElement>(
                Func<Account, IEnumerable<TCollection>> collectionFunc,
                BaseValidator<TElement> elementValidator,
                Func<TCollection, TElement> itemSelector)
                => throw new NotSupportedException();

            public ValidationRegistryRules<Account, TProperty> ValidateRegistryRules<TProperty>(Func<Account, TProperty> propertyFunc, BaseValidator<TProperty> validator)
                => throw new NotSupportedException();
        }

        private static Account ValidAccount() => new() { Email = "user@example.com", Nickname = "user" };

        [Fact]
        public async Task MustAsyncWithToken_RunsWithNoneToken_WhenValidatedWithoutOne()
        {
            // Arrange
            var observed = new CancellationToken(true);
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).MustAsync((email, token) =>
            {
                observed = token;

                return Task.FromResult(!string.IsNullOrEmpty(email));
            });

            // Act
            var result = await validator.ValidateAsync(ValidAccount());

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(CancellationToken.None, observed);
            Assert.False(observed.CanBeCanceled);
        }

        [Fact]
        public async Task MustAsyncWithToken_StillReportsFailure_WhenValidatedWithoutOne()
        {
            // Arrange
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email)
                .MustAsync((email, token) => Task.FromResult(!string.IsNullOrEmpty(email)))
                .WithMessage("Email is required.", "EMAIL_REQUIRED");

            // Act
            var result = await validator.ValidateAsync(new Account { Email = "" });

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Email", failure.PropertyName);
            Assert.Equal("Email is required.", failure.ErrorMessage);
            Assert.Equal("EMAIL_REQUIRED", failure.ErrorCode);
        }

        [Fact]
        public async Task MustAsyncWithToken_ReceivesTheTokenPassedToValidateAsync()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var observed = CancellationToken.None;
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).MustAsync((email, token) =>
            {
                observed = token;

                return Task.FromResult(true);
            });

            // Act
            var result = await validator.ValidateAsync(ValidAccount(), cts.Token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(cts.Token, observed);
            Assert.True(observed.CanBeCanceled);
        }

        [Fact]
        public async Task ValidateAsync_ThrowsOperationCanceled_AndSkipsRules_ForAnAlreadyCancelledToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var ruleRan = false;
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).MustAsync((email, token) =>
            {
                ruleRan = true;

                return Task.FromResult(true);
            });

            // Act
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => validator.ValidateAsync(ValidAccount(), cts.Token));

            // Assert
            Assert.Equal(cts.Token, exception.CancellationToken);
            Assert.False(ruleRan);
        }

        [Fact]
        public async Task ValidateAsync_LetsCancellationEscape_InsteadOfReportingItAsAFailure()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email)
                .MustAsync(async (email, token) =>
                {
                    cts.Cancel();
                    await Task.Yield();
                    token.ThrowIfCancellationRequested();

                    return true;
                })
                .WithMessage("Email is required.", "EMAIL_REQUIRED");

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => validator.ValidateAsync(ValidAccount(), cts.Token));
        }

        [Fact]
        public async Task ShouldAsyncWithToken_LetsCancellationEscape_InsteadOfReportingItAsAFailure()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).ShouldAsync(async (email, token) =>
            {
                cts.Cancel();
                await Task.Yield();
                token.ThrowIfCancellationRequested();
            }, "Email lookup failed.");

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => validator.ValidateAsync(ValidAccount(), cts.Token));
        }

        [Fact]
        public async Task ShouldAsyncWithToken_StillReportsOtherExceptionsAsFailures()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).ShouldAsync((email, token) =>
                throw new InvalidOperationException("boom"), "Email lookup failed.");

            // Act
            var result = await validator.ValidateAsync(ValidAccount(), cts.Token);

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Email", failure.PropertyName);
            Assert.Equal("Email lookup failed.", failure.ErrorMessage);
            Assert.Equal("ShouldRuleException", failure.ErrorCode);
        }

        [Fact]
        public async Task ShouldAsyncWithToken_StillReportsCancellationOfAnUnrelatedToken_AsAFailure()
        {
            // Arrange
            using var validationCts = new CancellationTokenSource();
            using var unrelatedCts = new CancellationTokenSource();
            unrelatedCts.Cancel();
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).ShouldAsync((email, token) =>
            {
                unrelatedCts.Token.ThrowIfCancellationRequested();

                return Task.CompletedTask;
            }, "Email lookup failed.");

            // Act
            var result = await validator.ValidateAsync(ValidAccount(), validationCts.Token);

            // Assert
            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Failures);
            Assert.Equal("Email lookup failed.", failure.ErrorMessage);
        }

        [Fact]
        public async Task ShouldAsyncWithCallbackAndToken_ReceivesTheTokenAndRecordsEveryError()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var observed = CancellationToken.None;
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Nickname).ShouldAsync((nickname, addError, token) =>
            {
                observed = token;
                addError("Nickname is reserved.");
                addError("Nickname is too short.");

                return Task.CompletedTask;
            });

            // Act
            var result = await validator.ValidateAsync(ValidAccount(), cts.Token);

            // Assert
            Assert.Equal(cts.Token, observed);
            Assert.False(result.IsValid);
            Assert.Equal(2, result.Failures.Count);
            Assert.All(result.Failures, failure => Assert.Equal("ShouldRule", failure.ErrorCode));
            Assert.Equal("Nickname is reserved.", result.Failures[0].ErrorMessage);
            Assert.Equal("Nickname is too short.", result.Failures[1].ErrorMessage);
        }

        [Fact]
        public async Task ShouldAsyncWithCallbackAndToken_LetsCancellationEscape_InsteadOfReportingItAsAFailure()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Nickname).ShouldAsync(async (nickname, addError, token) =>
            {
                cts.Cancel();
                await Task.Yield();
                token.ThrowIfCancellationRequested();
            });

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => validator.ValidateAsync(ValidAccount(), cts.Token));
        }

        [Fact]
        public async Task ValidateAsync_SurfacesCancellation_EvenWhenATokenLessShouldAsyncSwallowsIt()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).ShouldAsync(async email =>
            {
                cts.Cancel();
                await Task.Yield();
                cts.Token.ThrowIfCancellationRequested();
            }, "Email lookup failed.");

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => validator.ValidateAsync(ValidAccount(), cts.Token));
        }

        [Fact]
        public async Task TokenAwareRules_CountTowardsHasAsyncRules_AndKeepValidateThrowing()
        {
            // Arrange
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).MustAsync((email, token) => Task.FromResult(true));

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(ValidAccount()));
            var result = await validator.ValidateAsync(ValidAccount());

            // Assert
            Assert.True(validator.HasAsyncRules);
            Assert.Contains("ValidateAsync()", exception.Message);
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateAsync_WithToken_StillThrowsArgumentNullException_ForANullInstance()
        {
            // Arrange
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).IsNotEmpty();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => validator.ValidateAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task NestedValidator_PassesTheTokenToTheNestedRules()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var nested = new ProfileValidator();
            var validator = new AccountValidator();
            validator.ValidateNested(x => x.Profile, nested);

            // Act
            var result = await validator.ValidateAsync(
                new Account { Profile = new Profile { City = "Ankara" } },
                cts.Token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(cts.Token, nested.ObservedToken);
        }

        [Fact]
        public async Task CollectionValidator_PassesTheTokenToTheElementRules()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var elementValidator = new ProfileValidator();
            var validator = new AccountValidator();
            validator.ValidateCollection(x => x.Visited, elementValidator, profile => profile);

            // Act
            var result = await validator.ValidateAsync(
                new Account { Visited = { new Profile { City = "Ankara" } } },
                cts.Token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(cts.Token, elementValidator.ObservedToken);
        }

        [Fact]
        public async Task RegistryRules_PassTheTokenToTheReusedValidator()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var registered = new ProfileValidator();
            var validator = new AccountValidator();
            validator.ValidateRegistryRules(x => x.Profile!, registered);

            // Act
            var result = await validator.ValidateAsync(
                new Account { Profile = new Profile { City = "Ankara" } },
                cts.Token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(cts.Token, registered.ObservedToken);
        }

        [Fact]
        public async Task InterfaceReference_PassesTheTokenThroughToBaseValidatorRules()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var observed = CancellationToken.None;
            var validator = new AccountValidator();
            validator.RuleFor(x => x.Email).MustAsync((email, token) =>
            {
                observed = token;

                return Task.FromResult(true);
            });

            // Act
            IBaseValidator<Account> abstraction = validator;
            var result = await abstraction.ValidateAsync(ValidAccount(), cts.Token);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(cts.Token, observed);
        }

        [Fact]
        public async Task HandWrittenImplementation_KeepsWorking_ThroughTheDefaultInterfaceMethod()
        {
            // Arrange
            var handWritten = new HandWrittenValidator();
            IBaseValidator<Account> abstraction = handWritten;

            // Act
            var result = await abstraction.ValidateAsync(ValidAccount(), CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Hand written failure.", Assert.Single(result.Failures).ErrorMessage);
            Assert.Equal(1, handWritten.ValidateAsyncCalls);
        }

        [Fact]
        public async Task HandWrittenImplementation_ObservesTheToken_ThroughTheDefaultInterfaceMethod()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var handWritten = new HandWrittenValidator();
            IBaseValidator<Account> abstraction = handWritten;

            // Act
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => abstraction.ValidateAsync(ValidAccount(), cts.Token));

            // Assert
            Assert.Equal(cts.Token, exception.CancellationToken);
            Assert.Equal(0, handWritten.ValidateAsyncCalls);
        }
    }
}
