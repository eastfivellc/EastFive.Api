using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Http.Dispatcher;

namespace BlackBarLabs.Api
{
    class InjectableAssemblyResolver : IAssembliesResolver
    {
        private IAssembliesResolver wrapped;
        private Assembly inject;

        public InjectableAssemblyResolver(Assembly inject, IAssembliesResolver wrapped)
        {
            this.inject = inject;
            this.wrapped = wrapped;
        }

        public ICollection<Assembly> GetAssemblies()
        {
            ICollection<Assembly> baseAssemblies = wrapped.GetAssemblies();
            List<Assembly> assemblies = new List<Assembly>(baseAssemblies);
            baseAssemblies.Add(inject);
            return assemblies;
        }
    }
}
