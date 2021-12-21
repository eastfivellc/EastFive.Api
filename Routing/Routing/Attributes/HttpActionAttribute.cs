using EastFive.Api.Bindings;
using EastFive.Api.Meta.Flows;
using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpActionAttribute : HttpVerbAttribute, IProvideWorkflowUrl
    {
        public HttpActionAttribute(string method)
        {
            this.Action = method;
        }

        public string Action { get; set; }

        public override string Method => Action;

        public override bool IsMethodMatch(MethodInfo method, IHttpRequest request, IApplication httpApp,
            string[] componentsMatched)
        {
            var path = PathComponents(request)
                .Skip(componentsMatched.Length)
                .Select(segment => segment.Trim('/'.AsArray()))
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();
            if (!path.Any())
                return false;
            var action = path.First();

            var isMethodMatch = String.Compare(Action, action, true) == 0;
            return isMethodMatch;
        }

        protected override CastDelegate GetFileNameCastDelegate(
            IHttpRequest request, IApplication httpApp, string[] componentsMatched, out string[] pathKeys)
        {
            pathKeys = PathComponents(request)
                .Skip(componentsMatched.Length + 1)
                .ToArray();
            var paths = pathKeys;
            CastDelegate fileNameCastDelegate =
                (paramInfo, onParsed, onFailure) =>
                {
                    if (!paths.Any())
                        return onFailure("No URI filename value provided.");
                    if (paths.Length > 1)
                        return onFailure($"More than 1 path key `{paths.Join(',')}` not supported.");
                    return httpApp.Bind(paths.First(), paramInfo,
                        v => onParsed(v),
                        (why) => onFailure(why));
                };
            return fileNameCastDelegate;
        }

        public override Method GetMethod(Route route, MethodInfo methodInfo, HttpApplication httpApp)
        {
            var path = new Uri($"/{route.Namespace}/{route.Name}/{Action}", UriKind.Relative);
            return new Method(HttpMethod.Get.Method, methodInfo, route, path, httpApp);
        }

        public Url GetUrl(Api.Resources.Method method, QueryItem[] queryItems)
        {
            return new Url()
            {
                raw = $"{Url.VariableHostName}/{method.Route.Namespace}/{method.Route.Name}/{this.Action}",
                host = Url.VariableHostName.AsArray(),
                path = new string[] { method.Route.Namespace, method.Route.Name, this.Action },
                query = queryItems,
            };
        }
    }
}
