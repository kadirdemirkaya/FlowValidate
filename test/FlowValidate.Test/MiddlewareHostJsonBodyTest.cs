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
    public class MiddlewareHostJsonBodyTest
    {
        private const string ValidInvoiceJson = "{\"customer_name\":\"Acme\",\"total_amount\":10}";
        private const string InvalidInvoiceJson = "{\"customer_name\":\"\",\"total_amount\":10}";

        private static async Task<IHost> StartHostAsync(
            bool useSystemTextJson,
            Action<JsonSerializerOptions>? configureHostJson = null)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services
                            .AddControllers()
                            .AddApplicationPart(typeof(InvoicesController).Assembly)
                            .AddJsonOptions(options => configureHostJson?.Invoke(options.JsonSerializerOptions));

                        services.FlowValidationService(typeof(InvoicesController).Assembly);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        if (useSystemTextJson)
                        {
                            app.UseFlowValidation(options => options.UseSystemTextJson = true);
                        }
                        else
                        {
                            app.UseFlowValidation();
                        }

                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Build();

            await host.StartAsync();

            return host;
        }

        private static async Task<(HttpStatusCode Status, string ContentType, string Body)> PostAsync(
            IHost host,
            string path,
            string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await host.GetTestClient().PostAsync(path, content);

            return (
                response.StatusCode,
                response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task JsonPropertyNameModel_WithTheDefaultBodyReader_IsStillValidatedAsNull()
        {
            // Arrange
            using var host = await StartHostAsync(useSystemTextJson: false);

            // Act
            var response = await PostAsync(host, "/invoices/create", ValidInvoiceJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Contains("INVOICE_CUSTOMER_REQUIRED", response.Body);
        }

        [Fact]
        public async Task JsonPropertyNameModel_WithSystemTextJsonBody_ReachesTheController()
        {
            // Arrange
            using var host = await StartHostAsync(useSystemTextJson: true);

            // Act
            var response = await PostAsync(host, "/invoices/create", ValidInvoiceJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":\"Acme\"}", response.Body);
        }

        [Fact]
        public async Task JsonPropertyNameModel_WithSystemTextJsonBody_StillReportsAnInvalidValue()
        {
            // Arrange
            using var host = await StartHostAsync(useSystemTextJson: true);

            // Act
            var response = await PostAsync(host, "/invoices/create", InvalidInvoiceJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Equal("application/json", response.ContentType);

            using var body = JsonDocument.Parse(response.Body);
            var failure = Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Equal("CustomerName", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("Customer name is required.", failure.GetProperty("ErrorMessage").GetString());
            Assert.Equal("INVOICE_CUSTOMER_REQUIRED", failure.GetProperty("ErrorCode").GetString());
        }

        [Theory]
        [InlineData("{bad")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        [InlineData("{\"customer_name\":\"Acme\",\"total_amount\":\"not-a-number\"}")]
        public async Task SystemTextJsonBody_LeavesAnUnparsableBodyToModelBinding(string json)
        {
            // Arrange
            using var host = await StartHostAsync(useSystemTextJson: true);

            // Act
            var response = await PostAsync(host, "/invoices/create", json);

            // Assert
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.Status);
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);
            Assert.Contains("problem+json", response.ContentType);
            Assert.DoesNotContain("INVOICE_CUSTOMER_REQUIRED", response.Body);
        }

        [Fact]
        public async Task SystemTextJsonBody_UsesTheHostSerializerSettings()
        {
            // Arrange
            using var caseInsensitiveHost = await StartHostAsync(useSystemTextJson: true);
            using var caseSensitiveHost = await StartHostAsync(
                useSystemTextJson: true,
                configureHostJson: options => options.PropertyNameCaseInsensitive = false);

            // Act
            var caseInsensitive = await PostAsync(caseInsensitiveHost, "/payments/create", "{\"Name\":\"Pen\",\"quantity\":5}");
            var caseSensitive = await PostAsync(caseSensitiveHost, "/payments/create", "{\"Name\":\"Pen\",\"quantity\":5}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, caseInsensitive.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", caseInsensitive.Body);

            Assert.Equal(HttpStatusCode.BadRequest, caseSensitive.Status);
            Assert.Contains("ORDER_NAME_REQUIRED", caseSensitive.Body);
        }

        [Fact]
        public async Task SystemTextJsonBody_KeepsTheExistingBehaviorForAModelWithoutJsonAttributes()
        {
            // Arrange
            using var host = await StartHostAsync(useSystemTextJson: true);

            // Act
            var valid = await PostAsync(host, "/payments/create", "{\"name\":\"Pen\",\"quantity\":5}");
            var invalid = await PostAsync(host, "/payments/create", "{\"name\":\"\",\"quantity\":5}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, valid.Status);
            Assert.Equal("{\"accepted\":\"Pen\"}", valid.Body);

            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
            Assert.Equal("application/json", invalid.ContentType);

            using var body = JsonDocument.Parse(invalid.Body);
            var failure = Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Equal("Name", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("ORDER_NAME_REQUIRED", failure.GetProperty("ErrorCode").GetString());
        }

        [Fact]
        public void UseFlowValidation_WithoutAConfigureCallback_Throws()
        {
            // Arrange
            var services = new ServiceCollection();
            services.FlowValidationService(typeof(InvoicesController).Assembly);

            var app = new ApplicationBuilder(services.BuildServiceProvider());

            // Act
            var exception = Record.Exception(() => app.UseFlowValidation(configure: null!));

            // Assert
            Assert.IsType<ArgumentNullException>(exception);
        }
    }
}
