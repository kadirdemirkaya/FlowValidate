using FlowValidate.AspNetCore;
using FlowValidate.Extensions;
using FlowValidate.Test.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FlowValidate.Test
{
    public class MiddlewareMultiAssemblyTest
    {
        private const string InvalidOrderJson = "{\"name\":\"\",\"quantity\":5}";
        private const string ValidOrderJson = "{\"name\":\"Pen\",\"quantity\":5}";

        private sealed class FilteredAssembly : Assembly
        {
            private readonly Assembly _inner;
            private readonly Func<Type, bool> _typeFilter;

            public FilteredAssembly(Assembly inner, Func<Type, bool> typeFilter)
            {
                _inner = inner;
                _typeFilter = typeFilter;
            }

            public override Type[] GetTypes() => _inner.GetTypes().Where(_typeFilter).ToArray();
        }

        private static FilteredAssembly OnlyOrdersController()
        {
            return new FilteredAssembly(typeof(OrdersController).Assembly, t => t == typeof(OrdersController));
        }

        private static FilteredAssembly OnlyPaymentsController()
        {
            return new FilteredAssembly(typeof(PaymentsController).Assembly, t => t == typeof(PaymentsController));
        }

        private static async Task<IHost> StartHostAsync(Assembly firstRegisteredModule, Assembly secondRegisteredModule)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
                        services.FlowValidationService(typeof(OrdersController).Assembly);
                        services.FlowValidationAssemblies(firstRegisteredModule, secondRegisteredModule);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseFlowValidation();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Build();

            await host.StartAsync();

            return host;
        }

        private static async Task<(HttpStatusCode StatusCode, string Body)> PostAsync(IHost host, string path, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await host.GetTestClient().PostAsync(path, content);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private static string ReadFailureCode(string body)
        {
            using var document = JsonDocument.Parse(body);

            return Assert.Single(document.RootElement.GetProperty("Errors").EnumerateArray())
                .GetProperty("ErrorCode").GetString()!;
        }

        [Fact]
        public async Task FirstRegisteredModule_ControllerIsStillValidated()
        {
            // Arrange
            using var host = await StartHostAsync(OnlyOrdersController(), OnlyPaymentsController());

            // Act
            var response = await PostAsync(host, "/orders/create", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(response.Body));
        }

        [Fact]
        public async Task SecondRegisteredModule_ControllerIsAlsoValidated()
        {
            // Arrange
            using var host = await StartHostAsync(OnlyOrdersController(), OnlyPaymentsController());

            // Act
            var response = await PostAsync(host, "/payments/create", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(response.Body));
        }

        [Fact]
        public async Task BothRegisteredModules_ValidBodiesReachTheirControllers()
        {
            // Arrange
            using var host = await StartHostAsync(OnlyOrdersController(), OnlyPaymentsController());

            // Act
            var ordersResponse = await PostAsync(host, "/orders/create", ValidOrderJson);
            var paymentsResponse = await PostAsync(host, "/payments/create", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, paymentsResponse.StatusCode);
        }

        [Fact]
        public async Task SameModuleRegisteredTwice_DoesNotDuplicateTheFailure()
        {
            // Arrange
            var ordersModule = OnlyOrdersController();
            using var host = await StartHostAsync(ordersModule, ordersModule);

            // Act
            var response = await PostAsync(host, "/orders/create", InvalidOrderJson);
            using var body = JsonDocument.Parse(response.Body);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
        }

        [Fact]
        public async Task RepeatedRegistrationCalls_MergeAcrossCalls()
        {
            // Arrange
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
                        services.FlowValidationService(typeof(OrdersController).Assembly);
                        services.FlowValidationAssemblies(OnlyOrdersController());
                        services.FlowValidationAssemblies(OnlyPaymentsController());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseFlowValidation();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Build();

            await host.StartAsync();
            using var _ = host;

            // Act
            var ordersResponse = await PostAsync(host, "/orders/create", InvalidOrderJson);
            var paymentsResponse = await PostAsync(host, "/payments/create", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, ordersResponse.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, paymentsResponse.StatusCode);
        }

        [Fact]
        public async Task WithoutFlowValidationAssemblies_OnlyLastRegisteredControllerIsValidated()
        {
            // Arrange
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
                        services.FlowValidationService(typeof(OrdersController).Assembly);
                        services.FlowValidationService(OnlyOrdersController());
                        services.FlowValidationService(OnlyPaymentsController());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseFlowValidation();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Build();

            await host.StartAsync();
            using var _ = host;

            // Act
            var ordersResponse = await PostAsync(host, "/orders/create", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
        }
    }
}
