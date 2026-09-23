using FlowValidate.Builders;
using FlowValidate.Test.Models;
using FlowValidate.Test.Validators;
using System.Runtime.CompilerServices;

namespace FlowValidate.Test
{
    public class ValidatorTest
    {

        public class UserValidatorTests
        {
            private readonly UserValidator _validator = new();

            [Fact]
            public async Task Validate_Should_Pass_When_User_IsValid()
            {
                // Arrange
                var user = new User
                {
                    Name = "Kadir",
                    Age = 30,
                    Email = "test@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-5),
                    Tags = new() { "one", "two" },
                    UserDetails = new UserDetails
                    {
                        Address = "Istanbul - Turkey",
                        Phone = "1234567890"
                    }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.True(result.IsValid);
                Assert.Empty(result.Failures);
            }

            [Fact]
            public async Task Validate_Should_Fail_When_Name_IsEmpty()
            {
                // Arrange
                var user = new User
                {
                    Name = "",
                    Age = 25,
                    Email = "valid@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1),
                    Tags = new() { "one", "two" }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e => e.ErrorMessage.Contains("Name"));

            }

            [Fact]
            public async Task Validate_Should_Fail_When_Age_IsOutOfRange()
            {
                // Arrange
                var user = new User
                {
                    Name = "Test User",
                    Age = 70,
                    Email = "valid@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1)
                };

                // Act
                var result = await _validator.ValidateAsync(user);
                
                var error = result.Failures.ToList();

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e =>
                    (e.PropertyName != null && e.PropertyName.Contains("Age")) ||
                    (e.ErrorMessage != null && e.ErrorMessage.Contains("Validation failed"))
                );
            }

            [Fact]
            public async Task Validate_Should_Fail_When_Email_IsInvalid()
            {
                // Arrange
                var user = new User
                {
                    Name = "Test User",
                    Age = 25,
                    Email = "not-an-email",
                    PastTime = DateTime.UtcNow.AddDays(-1)
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e =>
                    (e.PropertyName != null && e.PropertyName.Contains("Email")) ||
                    (e.ErrorMessage != null && e.ErrorMessage.Contains("Email"))
                );

            }

            [Fact]
            public async Task Validate_Should_Fail_When_Tags_AreNotUnique()
            {
                // Arrange
                var user = new User
                {
                    Name = "Test User",
                    Age = 25,
                    Email = "valid@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1),
                    Tags = new() { "dup", "dup" }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e =>
                    (e.PropertyName != null && e.PropertyName.Contains("Tags")) ||
                    (e.ErrorMessage != null && e.ErrorMessage.Contains("Validation failed"))
                );


            }

            [Fact]
            public async Task Validate_Should_Fail_When_UserDetails_AreInvalid()
            {
                // Arrange
                var user = new User
                {
                    Name = "Test User",
                    Age = 25,
                    Email = "valid@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1),
                    UserDetails = new UserDetails
                    {
                        Address = "",
                        Phone = "12abc"
                    }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e =>
                    e.ErrorMessage != null && e.ErrorMessage.Contains("UserDetails address is required.")
                );

            }

            [Fact]
            public async Task Validate_Should_Pass_When_Baskets_AreValid()
            {
                // Arrange
                var user = new User
                {
                    Name = "Kadir",
                    Age = 25,
                    Email = "test@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1),
                    UserBaskets = new List<UserBasket>
                    {
                        new UserBasket { Name = "Sepet-1", Count = 5 },
                        new UserBasket { Name = "Sepet-2", Count = 20 }
                    }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.True(result.IsValid);
                Assert.Empty(result.Failures);
            }

            [Fact]
            public async Task Validate_Should_Fail_When_Basket_Name_IsEmpty()
            {
                // Arrange
                var user = new User
                {
                    Name = "Test User",
                    Age = 30,
                    Email = "test@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1),
                    UserBaskets = new List<UserBasket>
                    {
                        new UserBasket { Name = "", Count = 5 }
                    }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e => e.ErrorMessage.Contains("UserBaskets name is required."));
            }

            [Fact]
            public async Task Validate_Should_Fail_When_Basket_Count_IsOutOfRange()
            {
                // Arrange
                var user = new User
                {
                    Name = "Test User",
                    Age = 30,
                    Email = "test@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-1),
                    UserBaskets = new List<UserBasket>
                    {
                        new UserBasket { Name = "Sepet-1", Count = 0 },
                        new UserBasket { Name = "Sepet-2", Count = 150 }
                    }
                };

                // Act
                var result = await _validator.ValidateAsync(user);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, e => e.ErrorMessage.Contains("Validation failed"));
            }

            [Fact]
            public async Task Validate_Should_Handle_Nullable_Nickname()
            {
                // Arrange
                var user1 = new User { Name = "Kadir", Age = 25, Email = "test@test.com", Nickname = null };
                var user2 = new User { Name = "Kadir", Age = 25, Email = "test@test.com", Nickname = "Al" };
                var user3 = new User { Name = "Kadir", Age = 25, Email = "test@test.com", Nickname = "My Nick" };

                var validator = new UserValidator();

                // Act
                var result1 = await validator.ValidateAsync(user1);
                var result2 = await validator.ValidateAsync(user2);
                var result3 = await validator.ValidateAsync(user3);

                // Assert
                Assert.False(result1.IsValid);
                Assert.False(result2.IsValid);
                Assert.Contains(result2.Failures, e => e.ErrorMessage.Contains("Nickname must be at least 3 characters long"));
                Assert.False(result3.IsValid);
                Assert.Contains(result3.Failures, e => e.ErrorMessage.Contains("Nickname cannot contain spaces"));
            }

            [Fact]
            public void Validate_Synchronous_ShouldPass_ForPurelySynchronousValidator()
            {
                // Arrange
                var user = new User
                {
                    Name = "Kadir",
                    Age = 30,
                    Email = "test@test.com",
                    PastTime = DateTime.UtcNow.AddDays(-5),
                    Tags = new() { "one", "two" }
                };

                // Act
                var result = _validator.Validate(user);

                // Assert
                Assert.True(result.IsValid);
                Assert.Empty(result.Failures);
            }

        }

        public class AsyncValidationTests
        {
            private class AsyncTestModel
            {
                public string Email { get; set; } = string.Empty;
                public string Username { get; set; } = string.Empty;
                public string Description { get; set; } = string.Empty;
            }

            private class AsyncTestValidator : BaseValidator<AsyncTestModel>
            {
                public AsyncTestValidator(
                    Func<string, Task<bool>> emailCheck, 
                    Func<string, Action<string>, Task> usernameCheck, 
                    Func<string, Task> descriptionCheck)
                {
                    RuleFor(x => x.Email)
                        .MustAsync(emailCheck)
                        .WithMessage("Email check failed");

                    RuleFor(x => x.Username)
                        .ShouldAsync(usernameCheck);

                    RuleFor(x => x.Description)
                        .ShouldAsync(descriptionCheck, "Description check failed");
                }
            }

            [Fact]
            public async Task MustAsync_ShouldPass_WhenRuleReturnsTrue()
            {
                var model = new AsyncTestModel { Email = "test@example.com" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (u, addErr) => Task.CompletedTask,
                    d => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.True(result.IsValid);
                Assert.Empty(result.Failures);
            }

            [Fact]
            public async Task MustAsync_ShouldFail_WhenRuleReturnsFalse()
            {
                var model = new AsyncTestModel { Email = "test@example.com" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(false),
                    (u, addErr) => Task.CompletedTask,
                    d => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures);
                Assert.Contains(result.Failures, f => f.PropertyName == "Email" && f.ErrorMessage == "Email check failed");
            }

            [Fact]
            public async Task ShouldAsync_WithErrorCallback_ShouldPass_WhenNoErrorsAdded()
            {
                var model = new AsyncTestModel { Username = "validuser" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (username, addError) => Task.CompletedTask,
                    d => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.True(result.IsValid);
            }

            [Fact]
            public async Task ShouldAsync_WithErrorCallback_ShouldFail_WhenErrorAdded()
            {
                var model = new AsyncTestModel { Username = "invalid" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (username, addError) => { addError("Username is invalid"); return Task.CompletedTask; },
                    d => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, f => f.PropertyName == "Username" && f.ErrorMessage == "Username is invalid");
            }

            [Fact]
            public async Task ShouldAsync_WithErrorCallback_ShouldFail_WhenExceptionThrown()
            {
                var model = new AsyncTestModel { Username = "error" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (username, addError) => throw new InvalidOperationException("DB error"),
                    d => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, f => f.PropertyName == "Username" && f.ErrorCode == "ShouldRuleException");
            }

            [Fact]
            public async Task ShouldAsync_WithErrorCallback_ProducesExactlyOneFailure_WhenExceptionThrown()
            {
                var model = new AsyncTestModel { Username = "error" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (username, addError) => throw new InvalidOperationException("DB error"),
                    d => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures, f => f.PropertyName == "Username");
            }

            [Fact]
            public async Task ShouldAsync_WithDirectTask_ShouldPass_WhenNoExceptionThrown()
            {
                var model = new AsyncTestModel { Description = "Valid description" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (u, addErr) => Task.CompletedTask,
                    desc => Task.CompletedTask
                );

                var result = await validator.ValidateAsync(model);

                Assert.True(result.IsValid);
            }

            [Fact]
            public async Task ShouldAsync_WithDirectTask_ShouldFail_WhenExceptionThrown()
            {
                var model = new AsyncTestModel { Description = "Invalid description" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (u, addErr) => Task.CompletedTask,
                    desc => throw new ArgumentException("Bad length")
                );

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
                Assert.Contains(result.Failures, f => f.PropertyName == "Description" && f.ErrorMessage == "Description check failed");
            }

            [Fact]
            public async Task ShouldAsync_WithDirectTask_ProducesExactlyOneFailure_WhenExceptionThrown()
            {
                var model = new AsyncTestModel { Description = "Invalid description" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (u, addErr) => Task.CompletedTask,
                    desc => throw new ArgumentException("Bad length")
                );

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures, f => f.PropertyName == "Description");
            }

            [Fact]
            public void Validate_Synchronous_ShouldThrow_WhenAsyncRulesExist()
            {
                var model = new AsyncTestModel { Email = "test@example.com" };
                var validator = new AsyncTestValidator(
                    email => Task.FromResult(true),
                    (u, addErr) => Task.CompletedTask,
                    d => Task.CompletedTask
                );

                var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate(model));
                Assert.Contains("contains asynchronous rules", ex.Message);
            }
        }

        public class ShouldExceptionSingleFailureTests
        {
            private class ShouldExceptionTestModel
            {
                public string CallbackProp { get; set; } = string.Empty;
                public string MessageProp { get; set; } = string.Empty;
                public string ParamsProp { get; set; } = string.Empty;
                public string MustProp { get; set; } = string.Empty;
            }

            private class ShouldExceptionTestValidator : BaseValidator<ShouldExceptionTestModel>
            {
                public ShouldExceptionTestValidator()
                {
                    RuleFor(x => x.CallbackProp)
                        .Should((value, addError) => throw new InvalidOperationException("callback boom"));

                    RuleFor(x => x.MessageProp)
                        .Should(value => throw new InvalidOperationException("message boom"), "message rule failed");

                    RuleFor(x => x.ParamsProp)
                        .Should(value => throw new InvalidOperationException("params boom"), "params rule failed 1", "params rule failed 2");

                    RuleFor(x => x.MustProp)
                        .Must(value => false)
                        .WithMessage("must rule failed");
                }
            }

            [Fact]
            public void Should_WithErrorCallback_ProducesExactlyOneFailure_WhenExceptionThrown()
            {
                var validator = new ShouldExceptionTestValidator();
                var model = new ShouldExceptionTestModel();

                var result = validator.Validate(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures, f => f.PropertyName == "CallbackProp");
            }

            [Fact]
            public void Should_WithMessage_ProducesExactlyOneFailure_WhenExceptionThrown()
            {
                var validator = new ShouldExceptionTestValidator();
                var model = new ShouldExceptionTestModel();

                var result = validator.Validate(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures, f => f.PropertyName == "MessageProp");
            }

            [Fact]
            public void Should_WithParamsMessages_ProducesExactlyOneFailure_WhenExceptionThrown()
            {
                var validator = new ShouldExceptionTestValidator();
                var model = new ShouldExceptionTestModel();

                var result = validator.Validate(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures, f => f.PropertyName == "ParamsProp");
            }

            [Fact]
            public void Must_WithMessage_StillProducesExactlyOneFailure_WhenRuleFails()
            {
                var validator = new ShouldExceptionTestValidator();
                var model = new ShouldExceptionTestModel();

                var result = validator.Validate(model);

                Assert.False(result.IsValid);
                Assert.Single(result.Failures, f => f.PropertyName == "MustProp" && f.ErrorMessage == "must rule failed");
            }

            [Fact]
            public void Validate_ProducesExactlyFourFailures_OneForEachRule()
            {
                var validator = new ShouldExceptionTestValidator();
                var model = new ShouldExceptionTestModel();

                var result = validator.Validate(model);

                Assert.False(result.IsValid);
                Assert.Equal(4, result.Failures.Count);
            }
        }

        public class ShouldParamsSuccessTests
        {
            private class ShouldParamsSuccessTestModel
            {
                public string Name { get; set; } = string.Empty;
            }

            private class ShouldParamsSuccessTestValidator : BaseValidator<ShouldParamsSuccessTestModel>
            {
                public ShouldParamsSuccessTestValidator()
                {
                    RuleFor(x => x.Name)
                        .Should(value => { }, "m1", "m2");
                }
            }

            [Fact]
            public void Should_WithParamsMessages_ProducesNoFailures_WhenActionDoesNotThrow()
            {
                var validator = new ShouldParamsSuccessTestValidator();
                var model = new ShouldParamsSuccessTestModel { Name = "anything" };

                var result = validator.Validate(model);

                Assert.True(result.IsValid);
                Assert.Empty(result.Failures);
            }
        }

        public class GetAllErrorsTests
        {
            private class ObsoleteTestModel
            {
                public string Name { get; set; } = string.Empty;
            }

            private class ObsoleteTestValidator : BaseValidator<ObsoleteTestModel>
            {
                public ValidationRuleBuilder<ObsoleteTestModel, string> NameRuleBuilder;

                public ObsoleteTestValidator()
                {
                    NameRuleBuilder = RuleFor(x => x.Name).IsNotEmpty();
                }
            }

            [Fact]
            public async Task GetAllErrors_ReturnsEmptyString_AfterValidation()
            {
                var model = new ObsoleteTestModel { Name = string.Empty };
                var validator = new ObsoleteTestValidator();

                var result = await validator.ValidateAsync(model);

                Assert.False(result.IsValid);
#pragma warning disable CS0618
                Assert.Equal(string.Empty, validator.NameRuleBuilder.GetAllErrors());
#pragma warning restore CS0618
            }
        }
    }
}
