using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Razor
{
    public class RazorTemplateManager : ITemplateManager
    {
        public RazorTemplateManager()
        {
        }

        public ITemplateSource Resolve(ITemplateKey key)
        {
            string template = key.Name.TrimStart(new char[] { '\\', '~', '/' });
            var systemPath = System.Web.HttpRuntime.AppDomainAppPath;
            var viewsFile = new FileInfo($"{systemPath}Views\\{template}");
            if (viewsFile.Exists)
            {
                var path = viewsFile.FullName;
                string content = File.ReadAllText(path);
                return new LoadedTemplateSource(content, path);
            }
            var rootFile = new FileInfo($"{systemPath}\\{template}");
            if (rootFile.Exists)
            {
                var path = rootFile.FullName;
                string content = File.ReadAllText(path);
                return new LoadedTemplateSource(content, path);
            }
            string failureContent = $"<html><body>Could not file view with template:{key.Name}</body></html>";
            return new LoadedTemplateSource(failureContent, template);
        }

        public ITemplateKey GetKey(string name, ResolveType resolveType, ITemplateKey context)
        {
            return new NameOnlyTemplateKey(name, resolveType, context);
        }

        public void AddDynamic(ITemplateKey key, ITemplateSource source)
        {
            throw new NotImplementedException("dynamic templates are not supported!");
        }
        


    }


}
