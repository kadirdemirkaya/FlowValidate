using FlowValidate.AspNetCore;
using FlowValidate.Extensions;
using FlowValidate.Test.Controllers;
using FlowValidate.Test.Models;
using FlowValidate.Test.Validators;
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
    public class MiddlewareIndexedPropertyNameTest
    {
        private const string InvalidBasketJson = "{\"lines\":[{\"sku\":\"PEN\",\"qty\":1},{\"sku\":\"\",\"qty\":2}]}";
        private const string ValidBasketJson = "{\"lines\":[{\"sku\":\"PEN\",\"qty\":1}]}";

        private static async Task<IHost> StartHostAsync()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(BasketsController).Assembly);
                        services.FlowValidationService(typeof(BasketsController).Assembly);
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
            using var response = await host.GetTestClient().PostAsync("/baskets/create", content);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvalidCollectionElement_ReturnsBadRequestWithIndexedPropertyName()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, InvalidBasketJson);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.Status);

            using var document = JsonDocument.Parse(response.Body);
            var failure = Assert.Single(document.RootElement.GetProperty("Errors").EnumerateArray());

            Assert.Equal("Lines[1].Sku", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("Element 2: Sku is required.", failure.GetProperty("ErrorMessage").GetString());
            Assert.Equal("BASKET_LINE_SKU_REQUIRED", failure.GetProperty("ErrorCode").GetString());
        }

        [Fact]
        public async Task ValidBasket_PassesThroughToTheAction()
        {
            // Arrange
            using var host = await StartHostAsync();

            // Act
            var response = await PostAsync(host, ValidBasketJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Equal("{\"accepted\":1}", response.Body);
        }

        [Fact]
        public void IndexedPropertyNames_AreUsableAsToDictionaryKeys()
        {
            // Arrange
            var model = new Basket
            {
                Lines = new List<BasketLine>
                {
                    new BasketLine { Sku = "PEN", Qty = 1 },
                    new BasketLine { Sku = "", Qty = 2 }
                }
            };

            // Act
            var errors = new BasketValidator().Validate(model).ToDictionary();

            // Assert
            Assert.Equal(new[] { "Lines[1].Sku" }, errors.Keys);
            Assert.Equal(new[] { "Element 2: Sku is required." }, errors["Lines[1].Sku"]);
        }
    }
}
