using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FlowValidate.AspNetCore
{
    /// <summary>
    /// Registers additional assemblies for <see cref="FlowValidationMiddleware"/> to scan for controller
    /// actions, on top of the assembly registered via
    /// <see cref="FlowValidate.Extensions.FlowValidationExtensions.FlowValidationService"/>.
    /// </summary>
    public static class FlowValidationServiceCollectionExtensions
    {
        /// <summary>
        /// Adds <paramref name="assemblies"/> to the set of assemblies <see cref="FlowValidationMiddleware"/>
        /// scans for controllers. Safe to call more than once and with more than one assembly per call: every
        /// assembly passed across all calls is merged, and registering the same assembly again does not scan
        /// it twice. Call this in addition to
        /// <see cref="FlowValidate.Extensions.FlowValidationExtensions.FlowValidationService"/> when
        /// controllers for a request pipeline are spread across more than one assembly.
        /// </summary>
        /// <param name="services">The service collection to register the assemblies into.</param>
        /// <param name="assemblies">The assemblies scanned for controllers, in addition to the one already
        /// registered via <c>FlowValidationService</c>.</param>
        /// <returns><paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection FlowValidationAssemblies(this IServiceCollection services, params Assembly[] assemblies)
        {
            if (assemblies is null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var registry = GetOrAddRegistry(services);

            foreach (var assembly in assemblies)
            {
                if (assembly is not null)
                {
                    registry.Add(assembly);
                }
            }

            return services;
        }

        private static FlowValidationAssemblyRegistry GetOrAddRegistry(IServiceCollection services)
        {
            var existing = services
                .Select(d => d.ImplementationInstance)
                .OfType<FlowValidationAssemblyRegistry>()
                .FirstOrDefault();

            if (existing is not null)
            {
                return existing;
            }

            var registry = new FlowValidationAssemblyRegistry();
            services.AddSingleton(registry);

            return registry;
        }
    }
}
