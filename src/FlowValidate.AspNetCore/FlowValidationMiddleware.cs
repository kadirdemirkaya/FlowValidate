using FlowValidate.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Reflection;

namespace FlowValidate.AspNetCore
{
    public class FlowValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ActionParameterTypeCache _actionParameterTypes;
        private readonly IServiceProvider _serviceProvider;

        public FlowValidationMiddleware(RequestDelegate next, Assembly assembly, IServiceProvider serviceProvider)
        {
            _next = next;
            _actionParameterTypes = new ActionParameterTypeCache(assembly);
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var routeData = context.GetRouteData();
            var actionDescriptor = routeData.Values["action"] as string;
            var controllerDescriptor = routeData.Values["controller"] as string;

            if (!string.IsNullOrEmpty(actionDescriptor) && !string.IsNullOrEmpty(controllerDescriptor)
                && _actionParameterTypes.TryGetParameterTypes(controllerDescriptor, actionDescriptor, out var parameterTypes))
            {
                foreach (var modelType in parameterTypes)
                {
                    var validatorInterface = typeof(IBaseValidator<>);

                    context.Request.EnableBuffering();
                    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    var model = JsonConvert.DeserializeObject(requestBody, modelType);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var validatorType = validatorInterface.MakeGenericType(modelType);
                        var validator = scope.ServiceProvider.GetService(validatorType);

                        if (validator != null)
                        {
                            var method = validatorType.GetMethod("ValidateAsync");

                            if (method is null)
                            {
                                continue;
                            }

                            if (method.Invoke(validator, new[] { model }) is not Task<ValidationResult> task)
                            {
                                continue;
                            }

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
    }
}
