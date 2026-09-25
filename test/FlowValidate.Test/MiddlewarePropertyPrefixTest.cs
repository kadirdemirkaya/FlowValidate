using FlowValidate.AspNetCore;
using FlowValidate.Extensions;
using FlowValidate.Test.Controllers;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowValidate.Test
{
    public class MiddlewarePropertyPrefixTest
    {
        private const string InvalidCustomerJson = "{\"billing\":{\"city\":\"\"}}";
        private const string ValidCustomerJson = "{\"billing\":{\"city\":\"Istanbul\"}}";

        private static async Task<IHost> StartHostAsync()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(CustomersController).Assembly);
                        services.FlowValidationService(typeof(CustomersController).Assembly);
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

        private static async Task<(HttpStatusCode Status, string Body)> PostAsync(IHost host, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await host.GetTestClient().PostAsync("/customers/create", content);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvalidNestedProperty_ReturnsBadRequestWithPrefixedPropertyName()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, InvalidCustomerJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);

            using var document = JsonDocument.Parse(response.Body);
            var failure = Assert.Single(document.RootElement.GetProperty("Errors").EnumerateArray());

            Assert.Equal("Billing.City", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("City is required.", failure.GetProperty("ErrorMessage").GetString());
            Assert.Equal("CUSTOMER_ADDRESS_CITY_REQUIRED", failure.GetProperty("ErrorCode").GetString());
        }

        [Fact]
        public async Task ValidCustomer_PassesThroughToTheAction()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, ValidCustomerJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":\"Istanbul\"}", response.Body);
        }
    }
}
