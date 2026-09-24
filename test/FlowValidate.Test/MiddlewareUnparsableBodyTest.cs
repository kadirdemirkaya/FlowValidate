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
    public class MiddlewareUnparsableBodyTest
    {
        private const string ValidOrderJson = "{\"name\":\"Pen\",\"quantity\":5}";
        private const string InvalidOrderJson = "{\"name\":\"\",\"quantity\":5}";

        private static async Task<IHost> StartHostAsync()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(PaymentsController).Assembly);
                        services.FlowValidationService(typeof(PaymentsController).Assembly);
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

        private static async Task<(HttpStatusCode Status, string ContentType, string Body)> PostPaymentAsync(
            IHost host,
            string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await host.GetTestClient().PostAsync("/payments/create", content);

            return (
                response.StatusCode,
                response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("{bad")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        [InlineData("{\"name\":\"Pen\",\"quantity\":\"not-a-number\"}")]
        public async Task UnparsableBody_IsLeftToModelBinding_AndNeverReturnsServerError(string json)
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostPaymentAsync(host, json);

            // Assert
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.Status);
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Contains("problem+json", response.ContentType);
            Assert.DoesNotContain("ORDER_NAME_REQUIRED", response.Body);
        }

        [Fact]
        public async Task ParsableInvalidBody_IsStillAnsweredByTheValidationMiddleware()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostPaymentAsync(host, InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Equal("application/json", response.ContentType);

            using var body = JsonDocument.Parse(response.Body);
            var failure = Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Equal("Name", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("ORDER_NAME_REQUIRED", failure.GetProperty("ErrorCode").GetString());
        }

        [Fact]
        public async Task ValidBody_StillReachesTheControllerAction()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostPaymentAsync(host, ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", response.Body);
        }
    }
}
