using Microsoft.AspNetCore.Builder;

namespace FlowValidate.AspNetCore
{
    /// <summary>
    /// Options for <see cref="FlowValidationMiddleware"/>, configured through
    /// <see cref="FlowValidationApplicationBuilderExtensions.UseFlowValidation(IApplicationBuilder, System.Action{FlowValidationOptions})"/>.
    /// </summary>
    public class FlowValidationOptions
    {
        /// <summary>
        /// Deserializes the request body with <c>System.Text.Json</c> and the host's own serializer settings
        /// (<see cref="Microsoft.AspNetCore.Mvc.JsonOptions"/>, falling back to the ASP.NET Core web defaults when
        /// MVC is not registered), so the middleware validates the model exactly as model binding builds it —
        /// including <c>[JsonPropertyName]</c>, naming policies and custom converters.
        /// <para>
        /// Defaults to <see langword="false"/>, which keeps the <c>Newtonsoft.Json</c> deserialization used by
        /// earlier versions. Either way, a body that cannot be turned into a model — malformed JSON, an empty body,
        /// a literal <c>null</c> — is left to the host's model binding, and the response body of a failed
        /// validation is unchanged.
        /// </para>
        /// </summary>
        public bool UseSystemTextJson { get; set; }
    }
}
