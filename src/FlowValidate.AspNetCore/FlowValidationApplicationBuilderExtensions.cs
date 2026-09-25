using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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

        /// <summary>
        /// Registers <see cref="FlowValidationMiddleware"/> in the request pipeline with the options applied by
        /// <paramref name="configure"/>. Validation itself, and the <c>400 Bad Request</c> response body it writes,
        /// are the same as for <see cref="UseFlowValidation(IApplicationBuilder)"/>; the options only change how the
        /// request body is turned into the model that gets validated.
        /// </summary>
        /// <param name="app">The application builder to register the middleware into.</param>
        /// <param name="configure">Configures the options for this registration, e.g.
        /// <c>options =&gt; options.UseSystemTextJson = true</c> to deserialize the body with the host's own
        /// <c>System.Text.Json</c> settings instead of <c>Newtonsoft.Json</c>.</param>
        /// <returns><paramref name="app"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
        public static IApplicationBuilder UseFlowValidation(this IApplicationBuilder app, Action<FlowValidationOptions> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new FlowValidationOptions();
            configure(options);

            return app.Use(next =>
            {
                var middleware = new FlowValidationMiddleware(
                    next,
                    app.ApplicationServices.GetRequiredService<Assembly>(),
                    app.ApplicationServices,
                    options);

                return context => middleware.InvokeAsync(context);
            });
        }
    }
}
