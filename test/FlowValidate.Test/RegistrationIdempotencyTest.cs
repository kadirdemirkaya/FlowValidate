#pragma warning disable CS0618

using FlowValidate.Abstractions;
using FlowValidate.AspNetCore;
using FlowValidate.Extensions;
using FlowValidate.Middlewares;
using FlowValidate.Test.Controllers;
using FlowValidate.Test.Models;
using FlowValidate.Test.Validators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Reflection;
using System.Text;

namespace FlowValidate.Test
{
    public class RegistrationIdempotencyTest
    {
        private const string InvalidOrderJson = "{\"name\":\"\",\"quantity\":5}";

        private static readonly Assembly TestAssembly = typeof(OrdersController).Assembly;
        private static readonly Assembly CoreAssembly = typeof(ValidationResult).Assembly;

        public enum ValidationPipeline
        {
            CoreFlowValidationApp,
            AspNetCoreUseFlowValidation
        }

        private static ServiceCollection Register(params Assembly[] assemblies)
        {
            var services = new ServiceCollection();

            foreach (var assembly in assemblies)
            {
                services.FlowValidationService(assembly);
            }

            return services;
        }

        private static async Task<(HttpStatusCode StatusCode, string Body)> PostInvalidOrderAsync(ValidationPipeline pipeline, params Assembly[] assemblies)
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(TestAssembly);

                        foreach (var assembly in assemblies)
                        {
                            services.FlowValidationService(assembly);
                        }
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        if (pipeline == ValidationPipeline.CoreFlowValidationApp)
                        {
                            app.FlowValidationApp();
                        }
                        else
                        {
                            app.UseFlowValidation();
                        }

                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Build();

            await host.StartAsync();

            using var content = new StringContent(InvalidOrderJson, Encoding.UTF8, "application/json");
            using var response = await host.GetTestClient().PostAsync("/orders/create", content);
            var body = await response.Content.ReadAsStringAsync();

            await host.StopAsync();

            return (response.StatusCode, body);
        }

        private static string Describe(ServiceDescriptor descriptor)
        {
            return $"{descriptor.ServiceType.FullName}|{descriptor.Lifetime}|{descriptor.ImplementationType?.FullName}|{descriptor.ImplementationInstance}|{descriptor.ImplementationFactory is not null}";
        }

        [Fact]
        public void SameAssemblyTwice_RegistersSameDescriptorsAsSingleCall()
        {
            // Arrange
            var single = Register(TestAssembly);

            // Act
            var twice = Register(TestAssembly, TestAssembly);

            // Assert
            Assert.Equal(single.Count, twice.Count);
            Assert.Equal(single.Select(Describe), twice.Select(Describe));
            Assert.Single(twice, d => d.ServiceType == typeof(Assembly));
            Assert.Single(twice, d => d.ServiceType == typeof(IBaseValidator<Order>));
            Assert.Single(twice, d => d.ServiceType == typeof(ModelValidationMiddleware));
        }

        [Fact]
        public void SameAssemblyTwice_ResolvesEachValidatorOnce()
        {
            // Arrange
            using var provider = Register(TestAssembly, TestAssembly).BuildServiceProvider();
            using var scope = provider.CreateScope();

            // Act
            var validators = scope.ServiceProvider.GetServices<IBaseValidator<Order>>().ToList();
            var assemblies = provider.GetServices<Assembly>().ToList();

            // Assert
            Assert.IsType<OrderValidator>(Assert.Single(validators));
            Assert.Same(TestAssembly, Assert.Single(assemblies));
        }

        [Fact]
        public void DifferentAssemblies_RegisterAllInCallOrder()
        {
            // Arrange
            using var provider = Register(TestAssembly, CoreAssembly).BuildServiceProvider();
            using var scope = provider.CreateScope();

            // Act
            var assemblies = provider.GetServices<Assembly>().ToList();
            var validator = scope.ServiceProvider.GetService<IBaseValidator<Order>>();

            // Assert
            Assert.Equal(new[] { TestAssembly, CoreAssembly }, assemblies);
            Assert.Same(CoreAssembly, provider.GetRequiredService<Assembly>());
            Assert.IsType<OrderValidator>(validator);
        }

        [Fact]
        public void RepeatedAssemblyAfterAnother_StaysTheResolvedAssembly()
        {
            // Arrange
            using var provider = Register(TestAssembly, CoreAssembly, TestAssembly).BuildServiceProvider();

            // Act
            var resolved = provider.GetRequiredService<Assembly>();

            // Assert
            Assert.Same(TestAssembly, resolved);
            Assert.Equal(new[] { CoreAssembly, TestAssembly }, provider.GetServices<Assembly>());
        }

        [Fact]
        public void ValidatorLifetime_StaysScoped_WhenRegisteredTwice()
        {
            // Arrange
            using var provider = Register(TestAssembly, TestAssembly).BuildServiceProvider();
            using var firstScope = provider.CreateScope();
            using var secondScope = provider.CreateScope();

            // Act
            var firstA = firstScope.ServiceProvider.GetRequiredService<IBaseValidator<Order>>();
            var firstB = firstScope.ServiceProvider.GetRequiredService<IBaseValidator<Order>>();
            var second = secondScope.ServiceProvider.GetRequiredService<IBaseValidator<Order>>();

            // Assert
            Assert.Same(firstA, firstB);
            Assert.NotSame(firstA, second);
            Assert.All(
                Register(TestAssembly, TestAssembly).Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IBaseValidator<>)),
                d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
        }

        [Fact]
        public void KeyedAssemblyRegistration_IsLeftUntouched()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddKeyedSingleton("modules", TestAssembly);

            // Act
            services.FlowValidationService(TestAssembly);
            services.FlowValidationService(TestAssembly);
            using var provider = services.BuildServiceProvider();

            // Assert
            Assert.Same(TestAssembly, provider.GetRequiredKeyedService<Assembly>("modules"));
            Assert.Same(TestAssembly, Assert.Single(provider.GetServices<Assembly>()));
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task Middleware_UsesLastRegisteredAssembly_WhenItContainsTheController(ValidationPipeline pipeline)
        {
            // Act
            var response = await PostInvalidOrderAsync(pipeline, CoreAssembly, TestAssembly);

            // Assert
            Assert.Contains("ORDER_NAME_REQUIRED", response.Body);
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task Middleware_UsesLastRegisteredAssembly_WhenItLacksTheController(ValidationPipeline pipeline)
        {
            // Act
            var response = await PostInvalidOrderAsync(pipeline, TestAssembly, CoreAssembly);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"accepted\":\"\"}", response.Body);
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task Middleware_UsesRepeatedAssembly_WhenItIsRegisteredLastAgain(ValidationPipeline pipeline)
        {
            // Act
            var response = await PostInvalidOrderAsync(pipeline, TestAssembly, CoreAssembly, TestAssembly);

            // Assert
            Assert.Contains("ORDER_NAME_REQUIRED", response.Body);
        }
    }
}
