using System.Reflection;

namespace FlowValidate.AspNetCore
{
    internal sealed class FlowValidationAssemblyRegistry
    {
        private readonly object _gate = new object();
        private readonly HashSet<Assembly> _assemblies = new HashSet<Assembly>();

        public void Add(Assembly assembly)
        {
            lock (_gate)
            {
                _assemblies.Add(assembly);
            }
        }

        public IReadOnlyCollection<Assembly> Assemblies
        {
            get
            {
                lock (_gate)
                {
                    return _assemblies.ToArray();
                }
            }
        }
    }
}
