using EastFive.Api.Meta.OpenApi;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Resources
{
    [FunctionViewController(Route = "Manifest")]
    [OpenApiRoute(Collection = "EastFive.Api.Meta")]
    public class Manifest
    {
        [DataContract]
        public class WebIdManifest
        {

            public const string IdPropertyName = "id";
            [JsonProperty(PropertyName = IdPropertyName)]
            [DataMember(Name = IdPropertyName)]
            public BlackBarLabs.Api.Resources.WebId Id { get; set; }

            [JsonProperty(PropertyName = "endpoints")]
            public BlackBarLabs.Api.Resources.WebId[] Endpoints { get; set; }
        }

        public Manifest(IEnumerable<Type> lookups,
            HttpApplication httpApp)
        {
            this.Routes = lookups
                .Where(type => type.ContainsAttributeInterface<IDocumentRoute>())
                .Select(type => type.GetAttributesInterface<IDocumentRoute>()
                    .First()
                    .GetRoute(type, httpApp))
                .OrderBy(route => route.Name)
                .ToArray();
        }

        public Route[] Routes { get; set; }

        [EastFive.Api.HttpGet]
        public static IHttpResponse FindAsync(
                //Security security,
                HttpApplication application, IHttpRequest request, IProvideUrl url,
            ContentTypeResponse<WebIdManifest> onFound,
            ContentTypeResponse<Api.Resources.Manifest> onContent,
            ViewFileResponse<Api.Resources.Manifest> onHtml)
        {
            if (request.GetAcceptTypes().Where(accept => accept.MediaType.ToLower().Contains("html")).Any())
                return HtmlContent(application, request, url, onHtml);

            LocateControllers();
            var endpoints = Manifest.lookup
                .Select(
                    type =>
                    {
                        var endpoint = url.GetWebId(type, "x-com.orderowl:ordering");
                        return endpoint;
                    })
                .ToArray();

            var manifest = new WebIdManifest()
            {
                Id = Guid.NewGuid(),
                Endpoints = endpoints,
            };

            return onFound(manifest);
        }

        public static IHttpResponse HtmlContent(
                HttpApplication httpApp, IHttpRequest request, IProvideUrl url,
            ViewFileResponse<Api.Resources.Manifest> onHtml)
        {
            var lookups = httpApp.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);
            return onHtml("Manifest/Manifest.cshtml", manifest);
        }

        public static IHttpResponse ManifestContent(
                HttpApplication httpApp, System.Net.Http.HttpRequestMessage request, IProvideUrl url,
            ContentTypeResponse<Api.Resources.Manifest> onContent)
        {
            var lookups = httpApp.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);
            return onContent(manifest);
        }

        public static string GetRouteHtml(string route, KeyValuePair<System.Net.Http.HttpMethod, MethodInfo[]>[] methods)
        {
            var html = methods
                .Select(methodKvp => $"<div><h4>{methodKvp.Key}</h4>{GetMethodHtml(methodKvp.Key.Method, methodKvp.Value)}</div>")
                .Join("");
            return html;
        }

        public static string GetMethodHtml(string httpVerb, MethodInfo[] methods)
        {
            var html = methods
                .Select(
                    method =>
                    {
                        var parameterHtml = method
                            .GetParameters()
                            .Where(methodParam => methodParam.ContainsAttributeInterface<IBindApiValue>(true))
                            .Select(
                                methodParam =>
                                {
                                    var validator = methodParam.GetAttributeInterface<IBindApiValue>();
                                    var lookupName = validator.GetKey(methodParam);
                                    var required = methodParam.ContainsCustomAttribute<PropertyAttribute>() ||
                                        methodParam.ContainsCustomAttribute<QueryParameterAttribute>();

                                    return CSharpInvocationHtml(lookupName, required, methodParam.ParameterType);

                                })
                            .Join(",");
                        return $"<span class=\"method,csharp\">{method.Name}({parameterHtml})</span>";
                    })
                .Join("");
            return html;
        }

        public static string CSharpInvocationHtml(string name, bool required, Type parameterType)
        {
            var requiredString = required ? "[Required]" : "[Optional]";
            return $"<span>[{requiredString}]{parameterType.Name} <span>{name}</span></span>";
        }

        #region Load Controllers

        private static object lookupLock = new object();
        private static Type[] lookup;

        private static void LocateControllers()
        {
            lock (lookupLock)
            {
                if (!Manifest.lookup.IsDefaultNullOrEmpty())
                    return;

                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => (!assembly.GlobalAssemblyCache))
                    .ToArray();

                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    lock (lookupLock)
                    {
                        AddControllersFromAssembly(args.LoadedAssembly);
                    }
                };

                foreach (var assembly in loadedAssemblies)
                {
                    AddControllersFromAssembly(assembly);
                }
            }
        }


        private static void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly
                    .GetTypes();
                var results = types
                    .Where(type =>
                        type.GetCustomAttribute<FunctionViewControllerAttribute, bool>((attrs) => true, () => false))
                    .ToArray();

                Manifest.lookup = Manifest.lookup.NullToEmpty().Concat(results).ToArray();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

        #endregion
    }

    public class Response
    {
        public Response(ParameterInfo paramInfo)
        {
            this.ParamInfo = paramInfo;
            this.Name = paramInfo.Name;
            this.StatusCode = System.Net.HttpStatusCode.OK;
            //this.Example = "TODO: JSON serialize response type";
            this.Headers = new KeyValuePair<string, string>[] { };
        }

        public Response()
        {
        }

        public ParameterInfo ParamInfo { get; set; }

        public string Name { get; set; }

        public System.Net.HttpStatusCode StatusCode { get; set; }

        public string Example { get; set; }

        public KeyValuePair<string, string>[] Headers { get; set; }

        public bool IsMultipart
        {
            get
            {
                if (this.ParamInfo.ParameterType
                    .IsSubClassOfGeneric(typeof(MultipartAsyncResponse<>)))
                    return true;

                if (this.ParamInfo.ParameterType
                    .IsSubClassOfGeneric(typeof(MultipartAcceptArrayResponse<>)))
                    return true;

                return false;
            }
        }
    }
}
