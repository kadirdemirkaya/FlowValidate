#pragma warning disable CS0618

using FlowValidate.Abstractions;
using FlowValidate.AspNetCore;
using FlowValidate.Extensions;
using FlowValidate.Test.Controllers;
using FlowValidate.Test.Models;
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
    public class MiddlewareCancellationTest
    {
        private const string ValidTicketJson = "{\"name\":\"Broken lamp\"}";
        private const string InvalidTicketJson = "{\"name\":\"\"}";

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
                        services.AddControllers().AddApplicationPart(typeof(TicketsController).Assembly);
                        services.FlowValidationService(typeof(TicketsController).Assembly);
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

            return host;
        }

        private static async Task<HttpResponseMessage> PostTicketAsync(IHost host, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await host.GetTestClient().PostAsync("/tickets/create", content);
        }

        [Fact]
        public async Task UseFlowValidation_PassesRequestAbortedToTheValidator()
        {
            // Arrange
            RequestCancellationProbe.Reset();
            using var host = await StartHostAsync(ValidationPipeline.AspNetCoreUseFlowValidation);

            // Act
            using var response = await PostTicketAsync(host, ValidTicketJson);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body.RootElement.GetProperty("observed").GetBoolean());
            Assert.True(body.RootElement.GetProperty("canBeCanceled").GetBoolean());
            Assert.True(body.RootElement.GetProperty("matchesRequestAborted").GetBoolean());
        }

        [Fact]
        public async Task FlowValidationApp_StaysFrozen_AndValidatesWithoutAToken()
        {
            // Arrange
            RequestCancellationProbe.Reset();
            using var host = await StartHostAsync(ValidationPipeline.CoreFlowValidationApp);

            // Act
            using var response = await PostTicketAsync(host, ValidTicketJson);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body.RootElement.GetProperty("observed").GetBoolean());
            Assert.False(body.RootElement.GetProperty("canBeCanceled").GetBoolean());
        }

        [Theory]
        [InlineData(ValidationPipeline.CoreFlowValidationApp)]
        [InlineData(ValidationPipeline.AspNetCoreUseFlowValidation)]
        public async Task BothPipelines_StillResolveValidateAsync_AfterTheTokenOverloadWasAdded(ValidationPipeline pipeline)
        {
            // Arrange
            RequestCancellationProbe.Reset();
            using var host = await StartHostAsync(pipeline);

            // Act
            using var response = await PostTicketAsync(host, InvalidTicketJson);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // Assert
            var failure = Assert.Single(body.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Equal("Name", failure.GetProperty("PropertyName").GetString());
            Assert.Equal("Ticket name is required.", failure.GetProperty("ErrorMessage").GetString());
            Assert.Equal("TICKET_NAME_REQUIRED", failure.GetProperty("ErrorCode").GetString());
        }

        [Fact]
        public void ValidateAsync_IsAmbiguousByNameAlone_AndMustBeResolvedBySignature()
        {
            // Act & Assert
            Assert.Throws<AmbiguousMatchException>(() => typeof(IBaseValidator<Ticket>).GetMethod("ValidateAsync"));

            var tokenLess = typeof(IBaseValidator<Ticket>).GetMethod("ValidateAsync", new[] { typeof(Ticket) });
            var cancellable = typeof(IBaseValidator<Ticket>).GetMethod("ValidateAsync", new[] { typeof(Ticket), typeof(CancellationToken) });

            Assert.NotNull(tokenLess);
            Assert.Single(tokenLess!.GetParameters());
            Assert.NotNull(cancellable);
            Assert.Equal(2, cancellable!.GetParameters().Length);
        }
    }
}
