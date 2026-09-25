using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FlowValidate.AspNetCore
{
    internal sealed class ActionBodyParameterCache
    {
        private const string ControllerSuffix = "Controller";

        private readonly Lazy<IReadOnlyDictionary<string, Type>> _controllers;
        private readonly ConcurrentDictionary<Type, Lazy<HashSet<MethodInfo>>> _actions = new();
        private readonly ConcurrentDictionary<string, Type?> _bodyParameters = new();

        public ActionBodyParameterCache(IEnumerable<Assembly> assemblies)
        {
            _controllers = new Lazy<IReadOnlyDictionary<string, Type>>(() => IndexControllers(assemblies));
        }

        public bool TryGetBodyParameterType(
            ControllerActionDescriptor actionDescriptor,
            [MaybeNullWhen(false)] out Type bodyParameterType)
        {
            bodyParameterType = _bodyParameters.GetOrAdd(actionDescriptor.Id, _ => Resolve(actionDescriptor));

            return bodyParameterType is not null;
        }

        private Type? Resolve(ControllerActionDescriptor actionDescriptor)
        {
            var controllerTypeName = actionDescriptor.ControllerTypeInfo.FullName ?? actionDescriptor.ControllerTypeInfo.Name;

            if (!_controllers.Value.TryGetValue(controllerTypeName, out var controllerType))
            {
                return null;
            }

            var actions = _actions.GetOrAdd(
                controllerType,
                type => new Lazy<HashSet<MethodInfo>>(() => IndexActions(type))).Value;

            if (!actions.Contains(actionDescriptor.MethodInfo))
            {
                return null;
            }

            return ResolveBodyParameterType(actionDescriptor);
        }

        private static IReadOnlyDictionary<string, Type> IndexControllers(IEnumerable<Assembly> assemblies)
        {
            var controllers = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var assembly in assemblies.Distinct())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name.EndsWith(ControllerSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        controllers.TryAdd(type.FullName ?? type.Name, type);
                    }
                }
            }

            return controllers;
        }

        private static HashSet<MethodInfo> IndexActions(Type controllerType)
        {
            return new HashSet<MethodInfo>(controllerType.GetMethods());
        }

        private static Type? ResolveBodyParameterType(ActionDescriptor actionDescriptor)
        {
            Type? inferredBodyParameterType = null;
            var inferredCount = 0;

            foreach (var parameter in actionDescriptor.Parameters)
            {
                var bindingSource = parameter.BindingInfo?.BindingSource;

                if (bindingSource is not null)
                {
                    if (BindingSource.Body.Equals(bindingSource))
                    {
                        return parameter.ParameterType;
                    }

                    continue;
                }

                if (IsInferredBodyParameter(parameter.ParameterType))
                {
                    inferredBodyParameterType = parameter.ParameterType;
                    inferredCount++;
                }
            }

            return inferredCount == 1 ? inferredBodyParameterType : null;
        }

        private static bool IsInferredBodyParameter(Type parameterType)
        {
            if (parameterType == typeof(CancellationToken))
            {
                return false;
            }

            return !TypeDescriptor.GetConverter(parameterType).CanConvertFrom(typeof(string));
        }
    }
}
