using System.Collections.Concurrent;
using System.Reflection;

namespace FlowValidate.AspNetCore
{
    internal sealed class ActionParameterTypeCache
    {
        private const string ControllerSuffix = "Controller";

        private readonly Lazy<IReadOnlyDictionary<string, Type>> _controllers;
        private readonly ConcurrentDictionary<Type, Lazy<IReadOnlyDictionary<string, Type[]>>> _actions = new();

        public ActionParameterTypeCache(Assembly assembly)
        {
            _controllers = new Lazy<IReadOnlyDictionary<string, Type>>(() => IndexControllers(assembly));
        }

        public bool TryGetParameterTypes(string controllerName, string actionName, out Type[] parameterTypes)
        {
            parameterTypes = Array.Empty<Type>();

            if (!_controllers.Value.TryGetValue($"{controllerName}{ControllerSuffix}", out var controllerType))
            {
                return false;
            }

            var actions = _actions.GetOrAdd(
                controllerType,
                type => new Lazy<IReadOnlyDictionary<string, Type[]>>(() => IndexActions(type))).Value;

            if (!actions.TryGetValue(actionName, out var found))
            {
                return false;
            }

            parameterTypes = found;

            return true;
        }

        private static IReadOnlyDictionary<string, Type> IndexControllers(Assembly assembly)
        {
            var controllers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in assembly.GetTypes())
            {
                if (type.Name.EndsWith(ControllerSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    controllers.TryAdd(type.Name, type);
                }
            }

            return controllers;
        }

        private static IReadOnlyDictionary<string, Type[]> IndexActions(Type controllerType)
        {
            var actions = new Dictionary<string, Type[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var method in controllerType.GetMethods())
            {
                if (!actions.ContainsKey(method.Name))
                {
                    actions.Add(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray());
                }
            }

            return actions;
        }
    }
}
