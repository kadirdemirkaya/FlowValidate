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
        private readonly ConcurrentDictionary<Type, Lazy<IReadOnlyDictionary<string, Type?>>> _actions = new();

        public ActionBodyParameterCache(IEnumerable<Assembly> assemblies)
        {
            _controllers = new Lazy<IReadOnlyDictionary<string, Type>>(() => IndexControllers(assemblies));
        }

        public bool TryGetBodyParameterType(
            string controllerName,
            string actionName,
            [MaybeNullWhen(false)] out Type bodyParameterType)
        {
            bodyParameterType = null;

            if (!_controllers.Value.TryGetValue($"{controllerName}{ControllerSuffix}", out var controllerType))
            {
                return false;
            }

            var actions = _actions.GetOrAdd(
                controllerType,
                type => new Lazy<IReadOnlyDictionary<string, Type?>>(() => IndexActions(type))).Value;

            if (!actions.TryGetValue(actionName, out var found) || found is null)
            {
                return false;
            }

            bodyParameterType = found;

            return true;
        }

        private static IReadOnlyDictionary<string, Type> IndexControllers(IEnumerable<Assembly> assemblies)
        {
            var controllers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in assemblies.Distinct())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name.EndsWith(ControllerSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        controllers.TryAdd(type.Name, type);
                    }
                }
            }

            return controllers;
        }

        private static IReadOnlyDictionary<string, Type?> IndexActions(Type controllerType)
        {
            var actions = new Dictionary<string, Type?>(StringComparer.OrdinalIgnoreCase);

            foreach (var method in controllerType.GetMethods())
            {
                if (!actions.ContainsKey(method.Name))
                {
                    actions.Add(method.Name, ResolveBodyParameterType(method));
                }
            }

            return actions;
        }

        private static Type? ResolveBodyParameterType(MethodInfo method)
        {
            Type? inferredBodyParameterType = null;
            var inferredCount = 0;

            foreach (var parameter in method.GetParameters())
            {
                var bindingSource = GetBindingSource(parameter);

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

        private static BindingSource? GetBindingSource(ParameterInfo parameter)
        {
            foreach (var attribute in parameter.GetCustomAttributes(inherit: true))
            {
                if (attribute is IBindingSourceMetadata metadata && metadata.BindingSource is not null)
                {
                    return metadata.BindingSource;
                }
            }

            return null;
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
