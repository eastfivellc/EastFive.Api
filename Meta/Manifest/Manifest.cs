using EastFive.Api.Auth;
using EastFive.Api.Meta.OpenApi;
using EastFive.Extensions;
using EastFive.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

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
            public EastFive.Api.Resources.WebId Id { get; set; }

            [JsonProperty(PropertyName = "endpoints")]
            public EastFive.Api.Resources.WebId[] Endpoints { get; set; }
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

        [RequiredClaim(
            System.Security.Claims.ClaimTypes.Role,
            ClaimValues.Roles.SecurityReaderRoleId)]
        [HttpAction("Security")]
        public static IHttpResponse GetAttributes(
                [OptionalQueryParameter(Name = "untrusted_only")] bool? untrustedOnly,
                [OptionalQueryParameter(Name = "summary_only")] bool? summaryOnly,
                HttpApplication application, 
                IHttpRequest request, 
                IProvideUrl url,
            JsonStringResponse onJson)
        {
            var lookups = application.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, application);
            var result = manifest.Routes
                .SelectMany(route => route.Methods.NullToEmpty())
                .OrderBy(method => method.Path.ToString())
                .Select(method =>
                {
                    var methodAttr = method.MethodPoco
                        .GetCustomAttributes();
                    var hasSecAttribute = methodAttr
                        .Any(attr => application.IsSecurityAttribute(attr));
                    var hasSecParameter = method.MethodPoco
                        .GetParameters()
                        .Any(
                            (param) => 
                            {
                                // [Resource] makes any attribute behave CRUD-like so it skips over any security check
                                var isResource = param.GetCustomAttributes()
                                    .Any(attr => attr is ResourceAttribute);
                                
                                return !isResource && application.IsSecurityParameter(param);
                            });
                    var isUnsecured = methodAttr
                        .Any(attr => attr is UnsecuredAttribute);
                    var needsFurtherEvaluation = !hasSecAttribute && !hasSecParameter;
                    if ((untrustedOnly ?? false) && !needsFurtherEvaluation)
                        return null;

                    return new
                    {
                        verb = method.HttpMethod,
                        endpoint = method.Path.ToString(),
                        method = method.MethodPoco.DeclaringType.Namespace + "." + method.Route.Name + "." + method.Name,
                        secAttribute = hasSecAttribute ? 1 : 0, // more csv friendly than boolean
                        secParameter = hasSecParameter ? 1 : 0,
                        isUnsecured = isUnsecured ? 1 : 0,
                    };
                })
                .Where(obj => obj != null);
            if (summaryOnly ?? false)
            {
                var summary = new
                {
                    number_of_endpoints = result.Count(),
                    untrusted_endpoints = result.Where(r => r.secAttribute == 0 && r.secParameter == 0).Count(),
                    open_flow_endpoints = result.Where(r => r.isUnsecured == 1).Count(),
                    verb_summary = result
                        .GroupBy(r => r.verb.ToUpper())
                        .Select(g => new
                        {
                            verb = g.Key,
                            count = g.Count(),
                        }),
                };
                return onJson(JsonConvert.SerializeObject(summary, Formatting.Indented));
            }
            return onJson(JsonConvert.SerializeObject(result, Formatting.Indented));
        }

        [RequiredClaim(
            System.Security.Claims.ClaimTypes.Role,
            ClaimValues.Roles.SuperAdmin)]
        [HttpGet]
        public static IHttpResponse FindAsync(
                HttpApplication application, IHttpRequest request, IProvideUrl url,
            ContentTypeResponse<WebIdManifest> onFound,
            JsonStringResponse onJson,
            ViewFileResponse<Api.Resources.Manifest> onHtml)
        {
            if (request.GetAcceptTypes().Where(accept => accept.MediaType.ToLower().Contains("html")).Any())
                return HtmlContent(application, request, url, onHtml);

            LocateControllers(application.GetType());
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

        private static void LocateControllers(Type applicationType)
        {
            var limitedAssemblyQuery = applicationType
                .GetAttributesInterface<IApiResources>(inherit: true, multiple: true);

            lock (lookupLock)
            {
                if (!Manifest.lookup.IsDefaultNullOrEmpty())
                    return;

                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    lock (lookupLock)
                    {
                        AddControllersFromAssembly(args.LoadedAssembly);
                    }
                };

                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(ShouldCheckAssembly)
                    .ToArray();

                foreach (var assembly in loadedAssemblies)
                {
                    AddControllersFromAssembly(assembly);
                }
            }

            bool ShouldCheckAssembly(Assembly assembly)
            {
                return limitedAssemblyQuery
                    .First(
                        (limitedAssembly, next) =>
                        {
                            if (limitedAssembly.ShouldCheckAssembly(assembly))
                                return true;
                            return next();
                        },
                        () => false);
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

                Manifest.lookup = Manifest.lookup.NullToEmpty()
                    .Concat(results)
                    .Distinct(r => r.GUID)
                    .ToArray();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

        #endregion
    }

    public interface IProvideResponseType
    {
        Type GetResponseType(ParameterInfo parameterInfo);
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

        public bool IsMultipart { get; set; }
    }
}
