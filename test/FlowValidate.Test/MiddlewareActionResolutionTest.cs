using FlowValidate.AspNetCore;
using FlowValidate.Extensions;
using FlowValidate.Test.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FlowValidate.Test
{
    public class MiddlewareActionResolutionTest
    {
        private const string InvalidOrderJson = "{\"name\":\"\",\"quantity\":5}";
        private const string ValidOrderJson = "{\"name\":\"Pen\",\"quantity\":5}";
        private const string InvalidParcelJson = "{\"code\":\"\"}";
        private const string ValidParcelJson = "{\"code\":\"P-1\"}";

        private static async Task<IHost> StartHostAsync()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
                        services.FlowValidationService(typeof(OrdersController).Assembly);
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

        private static async Task<(HttpStatusCode Status, string Body)> PostAsync(IHost host, string path, string json)
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
        public async Task AsyncSuffixedAction_IsValidated()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var invalid = await PostAsync(host, "/shipments/save", InvalidOrderJson);
            var valid = await PostAsync(host, "/shipments/save", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(invalid.Body));
            Assert.Equal(HttpStatusCode.OK, valid.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", valid.Body);
        }

        [Fact]
        public async Task AsyncSuffixedAction_WithImplicitBodyParameter_IsValidated()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var invalid = await PostAsync(host, "/shipments/draft", InvalidOrderJson);
            var valid = await PostAsync(host, "/shipments/draft", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(invalid.Body));
            Assert.Equal(HttpStatusCode.OK, valid.Status);
        }

        [Fact]
        public async Task RenamedActionWithActionNameAttribute_IsValidated()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var invalid = await PostAsync(host, "/shipments/submit", InvalidOrderJson);
            var valid = await PostAsync(host, "/shipments/submit", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(invalid.Body));
            Assert.Equal(HttpStatusCode.OK, valid.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", valid.Body);
        }

        [Fact]
        public async Task SameControllerNameInTwoNamespaces_EachActionUsesItsOwnModel()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var invalidRetail = await PostAsync(host, "/retail/deliveries/create", InvalidOrderJson);
            var validRetail = await PostAsync(host, "/retail/deliveries/create", ValidOrderJson);
            var invalidWholesale = await PostAsync(host, "/wholesale/deliveries/create", InvalidParcelJson);
            var validWholesale = await PostAsync(host, "/wholesale/deliveries/create", ValidParcelJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, invalidRetail.Status);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(invalidRetail.Body));
            Assert.Equal(HttpStatusCode.OK, validRetail.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", validRetail.Body);
            Assert.Equal(HttpStatusCode.BadRequest, invalidWholesale.Status);
            Assert.Equal("PARCEL_CODE_REQUIRED", ReadFailureCode(invalidWholesale.Body));
            Assert.Equal(HttpStatusCode.OK, validWholesale.Status);
            Assert.Equal("{\"accepted\":\"P-1\"}", validWholesale.Body);
        }

        [Fact]
        public async Task RequestWithoutAnEndpoint_PassesThroughUntouched()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, "/not-a-route", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.Status);
            Assert.Equal(string.Empty, response.Body);
        }

        [Fact]
        public async Task MinimalApiEndpoint_IsNotValidated()
        {
            // Arrange
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
                        services.FlowValidationService(typeof(OrdersController).Assembly);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseFlowValidation();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapPost("/minimal/orders", () => "reached");
                        });
                    }))
                .Build();

            await host.StartAsync();
            using var _ = host;

            // Act
            var response = await PostAsync(host, "/minimal/orders", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("reached", response.Body);
        }
    }
}
