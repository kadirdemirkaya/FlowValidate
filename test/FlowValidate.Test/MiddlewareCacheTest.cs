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
    public class MiddlewareCacheTest
    {
        private const string InvalidOrderJson = "{\"name\":\"\",\"quantity\":5}";
        private const string ValidOrderJson = "{\"name\":\"Pen\",\"quantity\":5}";

        private sealed class CountingAssembly : Assembly
        {
            private readonly Assembly _inner;
            private readonly Func<Type, bool> _typeFilter;
            private readonly Func<MethodInfo, bool> _methodFilter;
            private int _getTypesCalls;
            private int _getMethodsCalls;

            public CountingAssembly(Assembly inner, Func<Type, bool> typeFilter, Func<MethodInfo, bool> methodFilter)
            {
                _inner = inner;
                _typeFilter = typeFilter;
                _methodFilter = methodFilter;
            }

            public int GetTypesCalls => Volatile.Read(ref _getTypesCalls);

            public int GetMethodsCalls => Volatile.Read(ref _getMethodsCalls);

            public override Type[] GetTypes()
            {
                Interlocked.Increment(ref _getTypesCalls);

                return _inner.GetTypes()
                    .Where(_typeFilter)
                    .Select(t => (Type)new CountingType(t, this))
                    .ToArray();
            }

            private sealed class CountingType : TypeDelegator
            {
                private readonly CountingAssembly _owner;

                public CountingType(Type delegatingType, CountingAssembly owner) : base(delegatingType)
                {
                    _owner = owner;
                }

                public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
                {
                    Interlocked.Increment(ref _owner._getMethodsCalls);

                    return base.GetMethods(bindingAttr).Where(_owner._methodFilter).ToArray();
                }
            }
        }

        private static CountingAssembly CountAllTypes()
        {
            return new CountingAssembly(typeof(OrdersController).Assembly, _ => true, _ => true);
        }

        private static async Task<IHost> StartHostAsync(Assembly middlewareAssembly)
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(OrdersController).Assembly);
                        services.FlowValidationService(typeof(OrdersController).Assembly);
                        services.AddSingleton(middlewareAssembly);
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

        private static async Task<(HttpStatusCode StatusCode, string Body)> PostOrderAsync(IHost host, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await host.GetTestClient().PostAsync("/orders/create", content);

            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private static string ReadFailureCode(string body)
        {
            using var document = JsonDocument.Parse(body);

            return Assert.Single(document.RootElement.GetProperty("Errors").EnumerateArray())
                .GetProperty("ErrorCode").GetString()!;
        }

        [Fact]
        public async Task RepeatedRequests_ScanAssemblyAndControllerOnlyOnce()
        {
            // Arrange
            var assembly = CountAllTypes();
            using var host = await StartHostAsync(assembly);

            // Act
            var first = await PostOrderAsync(host, InvalidOrderJson);
            var second = await PostOrderAsync(host, ValidOrderJson);
            var third = await PostOrderAsync(host, InvalidOrderJson);

            // Assert
            Assert.Equal(1, assembly.GetTypesCalls);
            Assert.Equal(1, assembly.GetMethodsCalls);
            Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
            Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(first.Body));
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal("{\"accepted\":\"Pen\"}", second.Body);
            Assert.Equal(first, third);
        }

        [Fact]
        public async Task ConcurrentRequests_ScanAssemblyAndControllerOnlyOnce()
        {
            // Arrange
            var assembly = CountAllTypes();
            using var host = await StartHostAsync(assembly);

            // Act
            var responses = await Task.WhenAll(Enumerable.Range(0, 32)
                .Select(i => PostOrderAsync(host, i % 2 == 0 ? InvalidOrderJson : ValidOrderJson)));

            // Assert
            Assert.Equal(1, assembly.GetTypesCalls);
            Assert.Equal(1, assembly.GetMethodsCalls);
            Assert.All(responses.Where((_, i) => i % 2 == 0), r =>
            {
                Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
                Assert.Equal("ORDER_NAME_REQUIRED", ReadFailureCode(r.Body));
            });
            Assert.All(responses.Where((_, i) => i % 2 == 1), r =>
            {
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                Assert.Equal("{\"accepted\":\"Pen\"}", r.Body);
            });
        }

        [Fact]
        public async Task ControllerMissingFromAssembly_IsSkippedWithoutRescanning()
        {
            // Arrange
            var assembly = new CountingAssembly(
                typeof(OrdersController).Assembly,
                t => t != typeof(OrdersController),
                _ => true);
            using var host = await StartHostAsync(assembly);

            // Act
            var first = await PostOrderAsync(host, InvalidOrderJson);
            var second = await PostOrderAsync(host, InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal("{\"accepted\":\"\"}", first.Body);
            Assert.Equal(first, second);
            Assert.Equal(1, assembly.GetTypesCalls);
            Assert.Equal(0, assembly.GetMethodsCalls);
        }

        [Fact]
        public async Task ActionMissingFromController_IsSkippedWithoutRescanning()
        {
            // Arrange
            var assembly = new CountingAssembly(
                typeof(OrdersController).Assembly,
                _ => true,
                m => m.Name != nameof(OrdersController.Create));
            using var host = await StartHostAsync(assembly);

            // Act
            var first = await PostOrderAsync(host, InvalidOrderJson);
            var second = await PostOrderAsync(host, InvalidOrderJson);

            // Assert
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal("{\"accepted\":\"\"}", first.Body);
            Assert.Equal(first, second);
            Assert.Equal(1, assembly.GetTypesCalls);
            Assert.Equal(1, assembly.GetMethodsCalls);
        }
    }
}
