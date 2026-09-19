using Microsoft.AspNetCore.Builder;

namespace FlowValidate.AspNetCore
{
    public static class FlowValidationApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseFlowValidation(this IApplicationBuilder app)
        {
            app.UseMiddleware<FlowValidationMiddleware>();

            return app;
        }
    }
}
