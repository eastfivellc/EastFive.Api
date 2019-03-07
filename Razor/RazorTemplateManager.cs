using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Razor
{
    public class RazorTemplateManager : ITemplateManager
    {
        private IDictionary<string, ITemplateSource> templateCache;

        public RazorTemplateManager()
        {
            templateCache = new Dictionary<string, ITemplateSource>();
        }

        public ITemplateSource Resolve(ITemplateKey key)
        {
            if (templateCache.ContainsKey(key.Name))
                return templateCache[key.Name];

            string template = key.Name.TrimStart(new char[] { '\\', '~', '/' });

            //Have to catch the error when using this via Azure Function
            var systemPath = string.Empty;
            try
            {
                systemPath = System.Web.HttpRuntime.AppDomainAppPath;
            }
            catch
            {
                systemPath = Environment.CurrentDirectory;
            }

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
            string failureContent = $"<html><body>Could not file view with template:{key.Name}  SystemPath:{systemPath}</body></html>";
            return new LoadedTemplateSource(failureContent, template);
        }

        public ITemplateKey GetKey(string name, ResolveType resolveType, ITemplateKey context)
        {
            return new NameOnlyTemplateKey(name, resolveType, context);
        }

        public void AddDynamic(ITemplateKey key, ITemplateSource source)
        {
            var resolved = Resolve(key);

            lock (templateCache)
            {
                if (!templateCache.ContainsKey(key.Name))
                    templateCache.Add(key.Name, resolved);
            }
        }
        


    }


}
