using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Razor
{
    public class GenericReferenceResolver : IReferenceResolver
    {
        public IEnumerable<CompilerReference> GetReferences(RazorEngine.Compilation.TypeContext context = null, IEnumerable<CompilerReference> includeAssemblies = null)
        {
            return CompilerServicesUtility
                   .GetLoadedAssemblies()
                   .Where(a => !a.IsDynamic && !a.FullName.Contains("Version=0.0.0.0") && File.Exists(a.Location) && !a.Location.Contains("CompiledRazorTemplates.Dynamic"))
                   .GroupBy(a => a.GetName().Name).Select(grp => grp.First(y => y.GetName().Version == grp.Max(x => x.GetName().Version))) // only select distinct assemblies based on FullName to avoid loading duplicate assemblies
                   .Select(a => CompilerReference.From(a))
                   .Concat(includeAssemblies ?? Enumerable.Empty<CompilerReference>());
        }
    }
}
