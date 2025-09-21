using FlowValidate.Abstractions;
using FlowValidate.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlowValidate.Extensions
{
    public static class FlowValidationExtensions
    {
        public static IServiceCollection FlowValidationService(this IServiceCollection services, Assembly assembly)
        {
            services.AddSingleton(assembly);

            var validatorType = typeof(IBaseValidator<>);

            var validators = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Interface = t.GetInterfaces()
                                 .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType),
                    Implementation = t
                })
                .Where(v => v.Interface != null);

            foreach (var validator in validators)
            {
                services.AddScoped(validator.Interface, validator.Implementation);
            }

            services.AddTransient<ModelValidationMiddleware>(sp => new ModelValidationMiddleware(
                 sp.GetRequiredService<RequestDelegate>(),
                 sp.GetRequiredService<Assembly>(),
                 sp
            ));

            return services;
        }

        public static IApplicationBuilder FlowValidationApp(this IApplicationBuilder app)
        {
            app.UseMiddleware<ModelValidationMiddleware>();

            return app;
        }
    }
}
