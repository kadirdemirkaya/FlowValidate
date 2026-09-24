using FlowValidate.Builders;
using System.Linq.Expressions;

namespace FlowValidate.Test
{
    public class IsUniqueRuleTest
    {
        private class Basket
        {
            public List<int>? IntTags { get; set; }
            public List<Guid>? GuidTags { get; set; }
            public int[]? IntArrayTags { get; set; }
            public List<string>? StringTags { get; set; }
        }

        private class BasketValidator : BaseValidator<Basket>
        {
        }

        private static bool IsValid<TProperty>(
            Basket model,
            Expression<Func<Basket, TProperty>> property)
        {
            var validator = new BasketValidator();
            validator.RuleFor(property).IsUnique();
            return validator.Validate(model).IsValid;
        }

        [Fact]
        public void IsUnique_ShouldPass_ForUniqueIntList()
        {
            var isValid = IsValid(new Basket { IntTags = new() { 1, 2 } }, x => x.IntTags);

            Assert.True(isValid);
        }

        [Fact]
        public void IsUnique_ShouldFail_ForDuplicateIntList()
        {
            var isValid = IsValid(new Basket { IntTags = new() { 1, 1 } }, x => x.IntTags);

            Assert.False(isValid);
        }

        [Fact]
        public void IsUnique_ShouldPass_ForUniqueGuidList()
        {
            var isValid = IsValid(new Basket { GuidTags = new() { Guid.NewGuid(), Guid.NewGuid() } }, x => x.GuidTags);

            Assert.True(isValid);
        }

        [Fact]
        public void IsUnique_ShouldFail_ForDuplicateGuidList()
        {
            var duplicate = Guid.NewGuid();
            var isValid = IsValid(new Basket { GuidTags = new() { duplicate, duplicate } }, x => x.GuidTags);

            Assert.False(isValid);
        }

        [Fact]
        public void IsUnique_ShouldPass_ForUniqueIntArray()
        {
            var isValid = IsValid(new Basket { IntArrayTags = new[] { 1, 2, 3 } }, x => x.IntArrayTags);

            Assert.True(isValid);
        }

        [Fact]
        public void IsUnique_ShouldFail_ForDuplicateIntArray()
        {
            var isValid = IsValid(new Basket { IntArrayTags = new[] { 1, 2, 1 } }, x => x.IntArrayTags);

            Assert.False(isValid);
        }

        [Fact]
        public void IsUnique_ShouldPass_ForUniqueStringList()
        {
            var isValid = IsValid(new Basket { StringTags = new() { "one", "two" } }, x => x.StringTags);

            Assert.True(isValid);
        }

        [Fact]
        public void IsUnique_ShouldFail_ForDuplicateStringList()
        {
            var isValid = IsValid(new Basket { StringTags = new() { "dup", "dup" } }, x => x.StringTags);

            Assert.False(isValid);
        }

        [Fact]
        public void IsUnique_ShouldFail_ForNullCollection()
        {
            var isValid = IsValid(new Basket { IntTags = null }, x => x.IntTags);

            Assert.False(isValid);
        }
    }
}
