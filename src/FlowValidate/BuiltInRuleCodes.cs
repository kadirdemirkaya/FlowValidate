namespace FlowValidate
{
    /// <summary>
    /// The <see cref="Models.ValidationFailure.ErrorCode"/> values FlowValidate produces itself, so a
    /// client can branch on a rule without comparing message text.
    /// </summary>
    /// <remarks>
    /// The per-rule codes are only reported when descriptive failures are switched on with
    /// <see cref="BaseValidator{T}.UseDescriptiveMessages"/> or
    /// <see cref="Builders.ValidationRuleBuilder{T, TProperty}.WithDescriptiveMessages"/>; otherwise a
    /// built-in rule keeps reporting <see cref="Default"/>. <see cref="Required"/>,
    /// <see cref="RegexTimeout"/>, <see cref="ShouldRule"/> and <see cref="ShouldRuleException"/> are
    /// reported either way.
    /// </remarks>
    public static class BuiltInRuleCodes
    {
        /// <summary>The code every rule reports when it has no code of its own: <c>DefaultRule</c>.</summary>
        public const string Default = "DefaultRule";

        /// <summary>The code reported by <c>RequiredIf</c>: <c>Required</c>.</summary>
        public const string Required = "Required";

        /// <summary>The code reported when a <c>MatchesRegex</c> match exceeds its timeout: <c>RegexTimeout</c>.</summary>
        public const string RegexTimeout = "RegexTimeout";

        /// <summary>The code reported for each error raised inside a <c>Should</c>/<c>ShouldAsync</c> callback: <c>ShouldRule</c>.</summary>
        public const string ShouldRule = "ShouldRule";

        /// <summary>The code reported when a <c>Should</c>/<c>ShouldAsync</c> callback throws without a message: <c>ShouldRuleException</c>.</summary>
        public const string ShouldRuleException = "ShouldRuleException";

        /// <summary>The descriptive code of <c>IsNotEmpty</c>: <c>NotEmpty</c>.</summary>
        public const string NotEmpty = "NotEmpty";

        /// <summary>The descriptive code of <c>IsEqual</c>: <c>Equal</c>.</summary>
        public const string Equal = "Equal";

        /// <summary>The descriptive code of <c>Contains</c>: <c>Contains</c>.</summary>
        public const string Contains = "Contains";

        /// <summary>The descriptive code of every <c>IsInRange</c> overload: <c>InRange</c>.</summary>
        public const string InRange = "InRange";

        /// <summary>The descriptive code of <c>IsEmail</c>: <c>Email</c>.</summary>
        public const string Email = "Email";

        /// <summary>The descriptive code of <c>Length</c>: <c>Length</c>.</summary>
        public const string Length = "Length";

        /// <summary>The descriptive code of every <c>IsGreaterThan</c> overload: <c>GreaterThan</c>.</summary>
        public const string GreaterThan = "GreaterThan";

        /// <summary>The descriptive code of every <c>IsLessThan</c> overload: <c>LessThan</c>.</summary>
        public const string LessThan = "LessThan";

        /// <summary>The descriptive code of every <c>MatchesRegex</c> overload: <c>RegexMatch</c>.</summary>
        public const string RegexMatch = "RegexMatch";

        /// <summary>The descriptive code of <c>IsDateInFuture</c>: <c>DateInFuture</c>.</summary>
        public const string DateInFuture = "DateInFuture";

        /// <summary>The descriptive code of <c>IsDateInPast</c>: <c>DateInPast</c>.</summary>
        public const string DateInPast = "DateInPast";

        /// <summary>The descriptive code of <c>IsInFuture</c>: <c>InFuture</c>.</summary>
        public const string InFuture = "InFuture";

        /// <summary>The descriptive code of <c>IsUnique</c>: <c>Unique</c>.</summary>
        public const string Unique = "Unique";

        /// <summary>The descriptive code of <c>NoConsecutiveRepeats</c>: <c>NoConsecutiveRepeats</c>.</summary>
        public const string NoConsecutiveRepeats = "NoConsecutiveRepeats";

        /// <summary>The descriptive code of <c>IsPalindrome</c>: <c>Palindrome</c>.</summary>
        public const string Palindrome = "Palindrome";

        /// <summary>The code reported by <c>ValidateCollection</c> for a <see langword="null"/> element: <c>NullElement</c>.</summary>
        public const string NullElement = "NullElement";
    }
}
