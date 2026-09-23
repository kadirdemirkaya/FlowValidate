#pragma warning disable CS0618

using FlowValidate.AspNetCore;
using FlowValidate.Enums;
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
    public class MiddlewareTest
    {
        public enum ValidationPipeline
        {
            CoreFlowValidationApp,
            AspNetCoreUseFlowValidation
        }

        private static async Task<IHost> StartHostAsync(ValidationPipeline pipeline)
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

                        if (pipeline == ValidationPipeline.CoreFlowValidationApp)
                        {
                            app.FlowValidationApp();
                        }
                        else
                        {
                            app.UseFlowValidation();
                        }

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapGet("/ping", () => "pong");
                        });
                    }))
                .Build();

            await host.StartAsync();

            return host;
        }

        private static async Task<HttpResponseMessage> PostOrderAsync(IHost host, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await host.GetTestClient().PostAsync("/orders/create", content);
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task InvalidBody_ShortCircuitsWithFailures(ValidationPipeline pipeline)
        {
            // Arrange
            using var host = await StartHostAsync(pipeline);

            // Act
            using var response = await PostOrderAsync(host, "{\"name\":\"\",\"quantity\":5}");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // Assert
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.False(body.RootElement.TryGetProperty("accepted", out _));
            var failure = Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Equal("Name", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("Order name is required.", failure.GetProperty("ErrorMessage").GetString());
            Assert.True(failure.TryGetProperty("AttemptedValue", out _));
            Assert.Equal("ORDER_NAME_REQUIRED", failure.GetProperty("ErrorCode").GetString());
            Assert.Equal((int)Severity.Error, failure.GetProperty("Severity").GetInt32());
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task ShouldRuleException_ProducesExactlyOneFailure_InResponseBody(ValidationPipeline pipeline)
        {
            // Arrange
            using var host = await StartHostAsync(pipeline);

            // Act
            using var response = await PostOrderAsync(host, "{\"name\":\"Pen\",\"quantity\":-1}");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // Assert
            var failure = Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Equal("Quantity", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("Quantity must not be negative.", failure.GetProperty("ErrorMessage").GetString());
        }

        [Fact]
        public async Task CoreFlowValidationApp_InvalidBody_KeepsOkStatus()
        {
            // Arrange
            using var host = await StartHostAsync(ValidationPipeline.CoreFlowValidationApp);

            // Act
            using var response = await PostOrderAsync(host, "{\"name\":\"\",\"quantity\":5}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UseFlowValidation_InvalidBody_RespondsWithBadRequest()
        {
            // Arrange
            using var host = await StartHostAsync(ValidationPipeline.AspNetCoreUseFlowValidation);

            // Act
            using var response = await PostOrderAsync(host, "{\"name\":\"\",\"quantity\":5}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UseFlowValidation_WritesSameFailureBodyAsFlowValidationApp()
        {
            // Arrange
            using var coreHost = await StartHostAsync(ValidationPipeline.CoreFlowValidationApp);
            using var aspNetCoreHost = await StartHostAsync(ValidationPipeline.AspNetCoreUseFlowValidation);

            // Act
            using var coreResponse = await PostOrderAsync(coreHost, "{\"name\":\"\",\"quantity\":5}");
            using var aspNetCoreResponse = await PostOrderAsync(aspNetCoreHost, "{\"name\":\"\",\"quantity\":5}");

            // Assert
            Assert.Equal(
                coreResponse.Content.Headers.ContentType?.ToString(),
                aspNetCoreResponse.Content.Headers.ContentType?.ToString());
            Assert.Equal(
                await coreResponse.Content.ReadAsStringAsync(),
                await aspNetCoreResponse.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task ValidBody_ReachesControllerAction(ValidationPipeline pipeline)
        {
            // Arrange
            using var host = await StartHostAsync(pipeline);

            // Act
            using var response = await PostOrderAsync(host, "{\"name\":\"Pen\",\"quantity\":5}");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Pen", body.RootElement.GetProperty("accepted").GetString());
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task BodyKeys_AreMatchedCaseInsensitively(ValidationPipeline pipeline)
        {
            // Arrange
            using var host = await StartHostAsync(pipeline);

            // Act
            using var response = await PostOrderAsync(host, "{\"NAME\":\"Pen\",\"QUANTITY\":5}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task RequestWithoutControllerRoute_PassesThrough(ValidationPipeline pipeline)
        {
            // Arrange
            using var host = await StartHostAsync(pipeline);

            // Act
            using var response = await host.GetTestClient().GetAsync("/ping");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("pong", await response.Content.ReadAsStringAsync());
        }
    }
}
