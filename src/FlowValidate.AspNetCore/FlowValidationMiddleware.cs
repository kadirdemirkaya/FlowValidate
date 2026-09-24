using FlowValidate.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FlowValidate.AspNetCore
{
    public class FlowValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ActionBodyParameterCache _actionBodyParameters;
        private readonly IServiceProvider _serviceProvider;

        public FlowValidationMiddleware(RequestDelegate next, Assembly assembly, IServiceProvider serviceProvider)
        {
            _next = next;
            _actionBodyParameters = new ActionBodyParameterCache(assembly);
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var routeData = context.GetRouteData();
            var actionDescriptor = routeData.Values["action"] as string;
            var controllerDescriptor = routeData.Values["controller"] as string;

            if (!string.IsNullOrEmpty(actionDescriptor) && !string.IsNullOrEmpty(controllerDescriptor)
                && _actionBodyParameters.TryGetBodyParameterType(controllerDescriptor, actionDescriptor, out var modelType))
            {
                context.Request.EnableBuffering();
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!TryDeserializeModel(requestBody, modelType, out var model))
                {
                    await _next(context);

                    return;
                }

                using (var scope = _serviceProvider.CreateScope())
                {
                    var validatorType = typeof(IBaseValidator<>).MakeGenericType(modelType);
                    var validator = scope.ServiceProvider.GetService(validatorType);

                    if (validator != null)
                    {
                        var method = validatorType.GetMethod("ValidateAsync");

                        if (method is not null && method.Invoke(validator, new[] { model }) is Task<ValidationResult> task)
                        {
                            var validationResult = await task;

                            if (!validationResult.IsValid)
                            {
                                context.Response.Clear();
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                context.Response.ContentType = "application/json";

                                var errors = validationResult.Failures.Select(f => new
                                {
                                    f.PropertyName,
                                    f.ErrorMessage,
                                    f.AttemptedValue,
                                    f.ErrorCode,
                                    f.Severity
                                });

                                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { Errors = errors }));
                                return;
                            }
                        }
                    }
                }
            }
            await _next(context);
        }

        private static bool TryDeserializeModel(
            string requestBody,
            Type modelType,
            [NotNullWhen(true)] out object? model)
        {
            model = null;

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return false;
            }

            try
            {
                model = JsonConvert.DeserializeObject(requestBody, modelType);
            }
            catch (JsonException)
            {
                return false;
            }

            return model is not null;
        }
    }
}
