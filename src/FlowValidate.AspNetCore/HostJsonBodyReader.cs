using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FlowValidate.AspNetCore
{
    internal static class HostJsonBodyReader
    {
        private static readonly JsonSerializerOptions WebDefaults = new(JsonSerializerDefaults.Web);

        internal static bool TryDeserialize(
            HttpContext context,
            string requestBody,
            Type modelType,
            [NotNullWhen(true)] out object? model)
        {
            model = null;

            try
            {
                model = JsonSerializer.Deserialize(requestBody, modelType, ResolveSerializerOptions(context));
            }
            catch (JsonException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            return model is not null;
        }

        private static JsonSerializerOptions ResolveSerializerOptions(HttpContext context)
        {
            var hostOptions = context.RequestServices?.GetService<IOptions<JsonOptions>>();

            return hostOptions?.Value.JsonSerializerOptions ?? WebDefaults;
        }
    }
}
