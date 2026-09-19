using FlowValidate.Builders;

namespace FlowValidate
{
    /// <summary>
    /// Range and comparison rules for any property type that implements <see cref="IComparable{T}"/>,
    /// such as <see cref="decimal"/>, <see cref="double"/>, <see cref="long"/> and <see cref="DateTime"/>,
    /// including their nullable forms.
    /// </summary>
    /// <remarks>
    /// Bounds are typed as the property itself, so a bound of a different type is a compile error rather than
    /// a rule that silently fails. The existing <c>int</c> overloads on <see cref="ValidationRuleBuilder{T, TProperty}"/>
    /// keep precedence whenever the bounds are <c>int</c> values. A <c>null</c> property value fails every rule here.
    /// </remarks>
    public static class ComparableRuleExtensions
    {
        /// <summary>
        /// Requires the property value to lie between <paramref name="minValue"/> and <paramref name="maxValue"/>, both inclusive.
        /// A <c>null</c> value fails the rule.
        /// </summary>
        /// <param name="builder">The rule builder of the property.</param>
        /// <param name="minValue">The smallest accepted value.</param>
        /// <param name="maxValue">The largest accepted value.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static ValidationRuleBuilder<T, TProperty> IsInRange<T, TProperty>(this ValidationRuleBuilder<T, TProperty> builder, TProperty minValue, TProperty maxValue)
            where TProperty : IComparable<TProperty>?
        {
            return builder.Must(value => value != null && value.CompareTo(minValue) >= 0 && value.CompareTo(maxValue) <= 0);
        }

        /// <summary>
        /// Requires the nullable property value to be present and to lie between <paramref name="minValue"/> and
        /// <paramref name="maxValue"/>, both inclusive. A <c>null</c> value fails the rule.
        /// </summary>
        /// <param name="builder">The rule builder of the nullable property.</param>
        /// <param name="minValue">The smallest accepted value.</param>
        /// <param name="maxValue">The largest accepted value.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static ValidationRuleBuilder<T, TProperty?> IsInRange<T, TProperty>(this ValidationRuleBuilder<T, TProperty?> builder, TProperty minValue, TProperty maxValue)
            where TProperty : struct, IComparable<TProperty>
        {
            return builder.Must(value => value.HasValue && value.Value.CompareTo(minValue) >= 0 && value.Value.CompareTo(maxValue) <= 0);
        }

        /// <summary>
        /// Requires the property value to be strictly greater than <paramref name="minValue"/>.
        /// A <c>null</c> value fails the rule.
        /// </summary>
        /// <param name="builder">The rule builder of the property.</param>
        /// <param name="minValue">The exclusive lower bound.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static ValidationRuleBuilder<T, TProperty> IsGreaterThan<T, TProperty>(this ValidationRuleBuilder<T, TProperty> builder, TProperty minValue)
            where TProperty : IComparable<TProperty>?
        {
            return builder.Must(value => value != null && value.CompareTo(minValue) > 0);
        }

        /// <summary>
        /// Requires the nullable property value to be present and strictly greater than <paramref name="minValue"/>.
        /// A <c>null</c> value fails the rule.
        /// </summary>
        /// <param name="builder">The rule builder of the nullable property.</param>
        /// <param name="minValue">The exclusive lower bound.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static ValidationRuleBuilder<T, TProperty?> IsGreaterThan<T, TProperty>(this ValidationRuleBuilder<T, TProperty?> builder, TProperty minValue)
            where TProperty : struct, IComparable<TProperty>
        {
            return builder.Must(value => value.HasValue && value.Value.CompareTo(minValue) > 0);
        }

        /// <summary>
        /// Requires the property value to be strictly less than <paramref name="maxValue"/>.
        /// A <c>null</c> value fails the rule.
        /// </summary>
        /// <param name="builder">The rule builder of the property.</param>
        /// <param name="maxValue">The exclusive upper bound.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static ValidationRuleBuilder<T, TProperty> IsLessThan<T, TProperty>(this ValidationRuleBuilder<T, TProperty> builder, TProperty maxValue)
            where TProperty : IComparable<TProperty>?
        {
            return builder.Must(value => value != null && value.CompareTo(maxValue) < 0);
        }

        /// <summary>
        /// Requires the nullable property value to be present and strictly less than <paramref name="maxValue"/>.
        /// A <c>null</c> value fails the rule.
        /// </summary>
        /// <param name="builder">The rule builder of the nullable property.</param>
        /// <param name="maxValue">The exclusive upper bound.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static ValidationRuleBuilder<T, TProperty?> IsLessThan<T, TProperty>(this ValidationRuleBuilder<T, TProperty?> builder, TProperty maxValue)
            where TProperty : struct, IComparable<TProperty>
        {
            return builder.Must(value => value.HasValue && value.Value.CompareTo(maxValue) < 0);
        }
    }
}
