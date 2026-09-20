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
    public class MiddlewareBodyParameterTest
    {
        private const string InvalidOrderJson = "{\"name\":\"\",\"quantity\":5}";
        private const string ValidOrderJson = "{\"name\":\"Pen\",\"quantity\":5}";

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

        private static (string PropertyName, string ErrorCode) ReadSingleFailure(string body)
        {
            using var document = JsonDocument.Parse(body);
            var failure = Assert.Single(document.RootElement.GetProperty("Errors").EnumerateArray());

            return (failure.GetProperty("PropertyName").GetString()!, failure.GetProperty("ErrorCode").GetString()!);
        }

        [Fact]
        public async Task SingleFromBodyParameter_KeepsValidatingTheBody()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var invalid = await PostAsync(host, "/orders/create", InvalidOrderJson);
            var valid = await PostAsync(host, "/orders/create", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
            Assert.Equal(("Name", "ORDER_NAME_REQUIRED"), ReadSingleFailure(invalid.Body));
            Assert.Equal(HttpStatusCode.OK, valid.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", valid.Body);
        }

        [Fact]
        public async Task MixedBinding_ValidBody_DoesNotValidateQueryBoundParameterFromBody()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, "/orders/search?term=pen", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":\"Pen\",\"term\":\"pen\"}", response.Body);
        }

        [Fact]
        public async Task MixedBinding_EmptyQueryBoundParameter_IsStillNotValidated()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, "/orders/search", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":\"Pen\",\"term\":\"\"}", response.Body);
        }

        [Fact]
        public async Task MixedBinding_InvalidBody_StillShortCircuitsWithTheBodyFailure()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, "/orders/search?term=pen", InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Equal(("Name", "ORDER_NAME_REQUIRED"), ReadSingleFailure(response.Body));
        }

        [Fact]
        public async Task MixedBinding_SimpleQueryParameter_IsNotDeserializedFromBody()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, "/orders/page?page=3", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":\"Pen\",\"page\":3}", response.Body);
        }

        [Fact]
        public async Task ImplicitSingleComplexParameter_KeepsBeingValidatedFromBody()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var invalid = await PostAsync(host, "/orders/draft", InvalidOrderJson);
            var valid = await PostAsync(host, "/orders/draft", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
            Assert.Equal(("Name", "ORDER_NAME_REQUIRED"), ReadSingleFailure(invalid.Body));
            Assert.Equal(HttpStatusCode.OK, valid.Status);
        }

        [Fact]
        public async Task ActionWithoutBodyBoundParameter_PassesTheRequestThrough()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, "/orders/lookup?term=pen", ValidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"term\":\"pen\"}", response.Body);
        }
    }
}
