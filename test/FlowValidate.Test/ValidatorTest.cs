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
                Assert.Empty(result.Errors);
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
                Assert.Contains(result.Errors, e => e.Contains("Name"));
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

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Errors, e => e.Contains("Age") || e.Contains("Validation failed "));
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
                Assert.Contains(result.Errors, e => e.Contains("Email"));
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
                Assert.Contains(result.Errors, e => e.Contains("Tags") ||e.Contains("Validation failed"));
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
                Assert.Contains(result.Errors, e => e.Contains("UserDetails address is required."));
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
                Assert.Empty(result.Errors);
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
                Assert.Contains(result.Errors, e => e.Contains("UserBaskets name is required."));
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
                Assert.Contains(result.Errors, e => e.Contains("Validation failed"));
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
                Assert.Contains(result2.Errors, e => e.Contains("Nickname must be at least 3 characters long"));
                Assert.False(result3.IsValid);
                Assert.Contains(result3.Errors, e => e.Contains("Nickname cannot contain spaces"));
            }

        }
    }
}
