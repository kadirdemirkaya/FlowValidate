using Microsoft.AspNetCore.Builder;

namespace FlowValidate.AspNetCore
{
    /// <summary>
    /// Request-pipeline registration extensions for the FlowValidate ASP.NET Core middleware.
    /// </summary>
    public static class FlowValidationApplicationBuilderExtensions
    {
        /// <summary>
        /// Registers <see cref="FlowValidationMiddleware"/> in the request pipeline. It validates
        /// controller action models against validators registered via
        /// <see cref="Extensions.FlowValidationExtensions.FlowValidationService"/> and responds with
        /// <c>400 Bad Request</c> and the validation failures when a model is invalid.
        /// </summary>
        /// <param name="app">The application builder to register the middleware into.</param>
        /// <returns><paramref name="app"/>, for chaining.</returns>
        public static IApplicationBuilder UseFlowValidation(this IApplicationBuilder app)
        {
            app.UseMiddleware<FlowValidationMiddleware>();

            return app;
        }
    }
}
