using FlowValidate.Abstractions;
using FlowValidate.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlowValidate.Extensions
{
    /// <summary>
    /// Dependency-injection registration extensions for FlowValidate validators.
    /// </summary>
    public static class FlowValidationExtensions
    {
#pragma warning disable CS0618
        private static readonly Func<IServiceProvider, ModelValidationMiddleware> ModelValidationMiddlewareFactory =
            sp => new ModelValidationMiddleware(
                sp.GetRequiredService<RequestDelegate>(),
                sp.GetRequiredService<Assembly>(),
                sp
            );
#pragma warning restore CS0618

        /// <summary>
        /// Scans <paramref name="assembly"/> for concrete <see cref="Abstractions.IBaseValidator{T}"/>
        /// implementations and registers each as scoped. Safe to call more than once (e.g. across
        /// multiple assemblies): a re-registration of the same assembly or validator replaces the
        /// previous one instead of duplicating it.
        /// </summary>
        /// <param name="services">The service collection to register validators into.</param>
        /// <param name="assembly">The assembly scanned for validator implementations.</param>
        /// <returns><paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection FlowValidationService(this IServiceCollection services, Assembly assembly)
        {
            ReplaceWithLatest(
                services,
                ServiceDescriptor.Singleton(typeof(Assembly), assembly),
                d => d.ServiceType == typeof(Assembly) && ReferenceEquals(d.ImplementationInstance, assembly));

            var validatorType = typeof(IBaseValidator<>);

            var validators = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Interface = t.GetInterfaces()
                                 .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType),
                    Implementation = t
                });

            foreach (var validator in validators)
            {
                if (validator.Interface is null)
                {
                    continue;
                }

                var serviceType = validator.Interface;
                var implementationType = validator.Implementation;

                ReplaceWithLatest(
                    services,
                    ServiceDescriptor.Scoped(serviceType, implementationType),
                    d => d.ServiceType == serviceType
                        && d.ImplementationType == implementationType
                        && d.Lifetime == ServiceLifetime.Scoped);
            }

#pragma warning disable CS0618
            ReplaceWithLatest(
                services,
                ServiceDescriptor.Transient(ModelValidationMiddlewareFactory),
                d => d.ServiceType == typeof(ModelValidationMiddleware)
                    && ReferenceEquals(d.ImplementationFactory, ModelValidationMiddlewareFactory));
#pragma warning restore CS0618

            return services;
        }

        private static void ReplaceWithLatest(IServiceCollection services, ServiceDescriptor descriptor, Func<ServiceDescriptor, bool> isSameRegistration)
        {
            for (var i = services.Count - 1; i >= 0; i--)
            {
                if (isSameRegistration(services[i]))
                {
                    services.RemoveAt(i);
                }
            }

            services.Add(descriptor);
        }

        /// <summary>
        /// Registers the core package's obsolete <c>ModelValidationMiddleware</c> in the request pipeline.
        /// </summary>
        /// <param name="app">The application builder to register the middleware into.</param>
        /// <returns><paramref name="app"/>, for chaining.</returns>
        [Obsolete("Use app.UseFlowValidation() from the FlowValidate.AspNetCore package. FlowValidationApp will be removed from the core package in the next major version.")]
        public static IApplicationBuilder FlowValidationApp(this IApplicationBuilder app)
        {
            app.UseMiddleware<ModelValidationMiddleware>();

            return app;
        }
    }
}
