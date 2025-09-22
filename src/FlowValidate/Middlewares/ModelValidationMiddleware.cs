using FlowValidate.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace FlowValidate.Middlewares
{
    public class ModelValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Assembly _assembly;
        private readonly IServiceProvider _serviceProvider;

        public ModelValidationMiddleware(RequestDelegate next, Assembly assembly, IServiceProvider serviceProvider)
        {
            _next = next;
            _assembly = assembly;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var routeData = context.GetRouteData();
            var actionDescriptor = routeData.Values["action"] as string;
            var controllerDescriptor = routeData.Values["controller"] as string;

            if (!string.IsNullOrEmpty(actionDescriptor) && !string.IsNullOrEmpty(controllerDescriptor))
            {
                var controllerType = _assembly.GetTypes()
                    .FirstOrDefault(t => t.Name.Equals($"{controllerDescriptor}Controller", StringComparison.OrdinalIgnoreCase));

                if (controllerType != null)
                {
                    var actionMethod = controllerType.GetMethods()
                        .FirstOrDefault(m => m.Name.Equals(actionDescriptor, StringComparison.OrdinalIgnoreCase));

                    if (actionMethod != null)
                    {
                        var parameters = actionMethod.GetParameters();

                        foreach (var parameter in parameters)
                        {
                            var modelType = parameter.ParameterType;

                            var validatorInterface = typeof(IBaseValidator<>);

                            context.Request.EnableBuffering(); // Request body'sini yeniden okumaya izin ver
                            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                            context.Request.Body.Position = 0; // Body konumunu sıfırlıyoruz

                            var model = JsonConvert.DeserializeObject(requestBody, modelType);

                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var validatorType = validatorInterface.MakeGenericType(modelType);
                                var validator = scope.ServiceProvider.GetService(validatorType);

                                if (validator != null)
                                {
                                    var method = validatorType.GetMethod("ValidateAsync");
                                    var task = (Task<ValidationResult>)method.Invoke(validator, new[] { model });
                                    var validationResult = await task;

                                    if (!validationResult.IsValid)
                                    {
                                        context.Response.StatusCode = StatusCodes.Status400BadRequest;

                                        context.Response.Clear();
                                        context.Response.ContentType = "application/json";

                                        var errors = new
                                        {
                                            validationResult.Errors
                                        };

                                        await context.Response.WriteAsync(JsonConvert.SerializeObject(errors));
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            await _next(context);
        }
    }
}
