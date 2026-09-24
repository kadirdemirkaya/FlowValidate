using FlowValidate.Enums;
using FlowValidate.Models;

namespace FlowValidate.Test
{
    public class ValidationResultToDictionaryTest
    {
        [Fact]
        public void ToDictionary_OnSuccessResult_ReturnsEmptyDictionary()
        {
            // Arrange
            var result = ValidationResult.Success();

            // Act
            var dictionary = result.ToDictionary();

            // Assert
            Assert.Empty(dictionary);
        }

        [Fact]
        public void ToDictionary_SinglePropertyMultipleFailures_GroupsMessagesInOrder()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddFailure("must not be empty", "Name");
            result.AddFailure("must be at least 3 characters", "Name");
            result.AddFailure("must not contain digits", "Name");

            // Act
            var dictionary = result.ToDictionary();

            // Assert
            var messages = Assert.Single(dictionary).Value;
            Assert.Equal("Name", Assert.Single(dictionary).Key);
            Assert.Equal(new[] { "must not be empty", "must be at least 3 characters", "must not contain digits" }, messages);
        }

        [Fact]
        public void ToDictionary_MultipleProperties_GroupsEachSeparately()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddFailure("must not be empty", "Name");
            result.AddFailure("must be a valid email", "Email");
            result.AddFailure("must be positive", "Age");

            // Act
            var dictionary = result.ToDictionary();

            // Assert
            Assert.Equal(3, dictionary.Count);
            Assert.Equal(new[] { "must not be empty" }, dictionary["Name"]);
            Assert.Equal(new[] { "must be a valid email" }, dictionary["Email"]);
            Assert.Equal(new[] { "must be positive" }, dictionary["Age"]);
        }

        [Fact]
        public void ToDictionary_PreservesInsertionOrderOfKeys()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddFailure("error c", "C");
            result.AddFailure("error a", "A");
            result.AddFailure("error b", "B");
            result.AddFailure("error c 2", "C");

            // Act
            var dictionary = result.ToDictionary();

            // Assert
            Assert.Equal(new[] { "C", "A", "B" }, dictionary.Keys);
            Assert.Equal(new[] { "error c", "error c 2" }, dictionary["C"]);
        }

        [Fact]
        public void ToDictionary_NullOrEmptyPropertyName_GroupsUnderRootKey()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddFailure(new ValidationFailure("root failure one", null));
            result.AddFailure(new ValidationFailure("root failure two", ""));

            // Act
            var dictionary = result.ToDictionary();

            // Assert
            var key = Assert.Single(dictionary).Key;
            Assert.Equal("<root>", key);
            Assert.Equal(new[] { "root failure one", "root failure two" }, dictionary["<root>"]);
        }

        [Fact]
        public void ToDictionary_IncludesAllSeverities_NotJustErrors()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddFailure(new ValidationFailure("warning message", "Name", severity: Severity.Warning));

            // Act
            var dictionary = result.ToDictionary();

            // Assert
            Assert.Equal(new[] { "warning message" }, dictionary["Name"]);
        }
    }
}
