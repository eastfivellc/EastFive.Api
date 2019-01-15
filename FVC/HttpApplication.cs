using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using EastFive.Linq;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api.Resources;
using System.Web.Http.Routing;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using System.IO;
using BlackBarLabs;
using System.Threading;
using System.Web.Http;
using EastFive.Web;
using EastFive.Linq.Async;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RazorEngine.Templating;
using EastFive.Reflection;

namespace EastFive.Api
{
    public interface IParseToken
    {
        bool IsString { get; }
        string ReadString();

        byte[] ReadBytes();

        Stream ReadStream();

        IParseToken[] ReadArray();

        IDictionary<string, IParseToken> ReadDictionary();
        T ReadObject<T>();
    }

    public struct ParseToken : IParseToken
    {
        private string value;

        public ParseToken(string value)
        {
            this.value = value;
        }

        public bool IsString => true;

        public IParseToken[] ReadArray()
        {
            throw new NotImplementedException();
        }

        public byte[] ReadBytes()
        {
            return System.Text.Encoding.UTF8.GetBytes(value);
        }

        public IDictionary<string, IParseToken> ReadDictionary()
        {
            throw new NotImplementedException();
        }

        public T ReadObject<T>()
        {
            throw new NotImplementedException();
        }

        public Stream ReadStream()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            return new MemoryStream(bytes);
        }

        public string ReadString()
        {
            return this.value;
        }
    }

    public class HttpApplication : System.Web.HttpApplication
    {
        private Task initialization;
        private ManualResetEvent initializationLock;

        public HttpApplication()
            : base()
        {
            initializationLock = new ManualResetEvent(false);
            this.initialization = InitializeAsync();
        }

        public virtual string Namespace
        {
            get
            {
                return "";
            }
        }

        protected void Application_Start()
        {
            System.Web.Mvc.AreaRegistration.RegisterAllAreas();
            ApplicationStart();
            GlobalConfiguration.Configure(this.Configure);
            Registration();

            var templateManager = new Razor.RazorTemplateManager();
            var config = new RazorEngine.Configuration.TemplateServiceConfiguration
            {
                TemplateManager = templateManager,
                BaseTemplateType = typeof(Razor.HtmlSupportTemplateBase<>)
            };
            RazorEngine.Engine.Razor = RazorEngine.Templating.RazorEngineService.Create(config);
        }

        public virtual void ApplicationStart()
        {
            LocateControllers();
        }

        protected virtual void Registration()
        {
        }

        protected virtual void Configure(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.MessageHandlers.Add(new Modules.ControllerHandler(config));
            config.MessageHandlers.Add(new Modules.MonitoringHandler(config));
        }

        protected class Initialized
        {
            private Initialized()
            {

            }

            internal static Initialized Create()
            {
                return new Initialized();
            }
        }

        protected virtual Task<Initialized> InitializeAsync()
        {
            initializationLock.Set();
            return Initialized.Create().ToTask();
        }

        protected void InitializationWait()
        {
            initializationLock.WaitOne();
        }

        #region Url Handlers

        public TResult GetResourceType<TResult>(string resourceType,
            Func<Type, TResult> onConverted,
            Func<TResult> onMatchingResourceNotFound)
        {
            return contentTypeLookup.First(
                (kvp, next) =>
                {
                    if (!kvp.Value.Contains(resourceType))
                        return next();
                    return onConverted(kvp.Key);
                },
                () => onMatchingResourceNotFound());
        }
        
        public string GetResourceMime(Type type)
        {
            if (type.IsDefaultOrNull())
                return $"x-application/resource";
            if (!contentTypeLookup.ContainsKey(type))
                return $"x-application/resource";
            return contentTypeLookup[type];
        }

        public WebId GetResourceLink(string resourceType, Guid? resourceIdMaybe, UrlHelper url)
        {
            if (!resourceIdMaybe.HasValue)
                return default(WebId);
            return GetResourceLink(resourceType, resourceIdMaybe.Value, url);
        }

        public delegate Task StoreMonitoringDelegate(Guid monitorRecordId, Guid authenticationId, DateTime when, string method, string controllerName, string queryString);

        public virtual TResult DoesStoreMonitoring<TResult>(
            Func<StoreMonitoringDelegate, TResult> onMonitorUsingThisCallback,
            Func<TResult> onNoMonitoring)
        {
            return onNoMonitoring();
        }

        public WebId GetResourceLink(string resourceType, Guid resourceId, UrlHelper url)
        {
            var id = new WebId
            {
                Key = resourceId.ToString("N"),
                UUID = resourceId,
                URN = new Uri($"urn:{resourceType}/{resourceId}")
            };
            return id
                .IfThen(
                    (!resourceType.IsNullOrWhiteSpace()) && resourceNameControllerLookup.ContainsKey(resourceType),
                    (webId) =>
                    {
                        var controllerType = resourceNameControllerLookup[resourceType];
                        webId.Source = url.GetLocation(controllerType, resourceId);
                        return webId;
                    });
        }

        public WebId GetResourceLink(Type resourceType, Guid? resourceIdMaybe, UrlHelper url)
        {
            if (!resourceIdMaybe.HasValue)
                return default(WebId);
            return GetResourceLink(resourceType, resourceIdMaybe.Value, url);
        }

        public WebId GetResourceLink(Type resourceType, Guid resourceId, UrlHelper url)
        {
            var id = new WebId
            {
                Key = resourceId.ToString("N"),
                UUID = resourceId,
                URN = new Uri($"urn:{GetResourceMime(resourceType)}/{resourceId}")
            };
            return id
                .IfThen(
                    (!resourceType.IsDefaultOrNull()) && resourceTypeControllerLookup.ContainsKey(resourceType),
                    (webId) =>
                    {
                        var controllerType = resourceTypeControllerLookup[resourceType];
                        webId.Source = url.GetLocation(controllerType, resourceId);
                        return webId;
                    });
        }

        public delegate Uri ControllerHandlerDelegate(
                HttpApplication httpApp, UrlHelper urlHelper, ParameterInfo parameterInfo,
            Func<Type, string, Uri> onSuccess);
        
        public KeyValuePair<string, KeyValuePair<HttpMethod, MethodInfo[]>[]>[] GetLookups()
        {
            return lookup.Select(kvp => kvp.Key.PairWithValue(kvp.Value.ToArray())).ToArray();
        }

        internal TResult GetControllerMethods<TResult>(string routeName,
            Func<IDictionary<HttpMethod, MethodInfo[]>, TResult> onMethodsIdentified, 
            Func<TResult> onKeyNotFound)
        {
            var routeNameLower = routeName.ToLower();
            if (!lookup.ContainsKey(routeNameLower))
                return onKeyNotFound();
            var possibleHttpMethods = lookup[routeNameLower];
            return onMethodsIdentified(possibleHttpMethods);
        }

        public Dictionary<Type, ControllerHandlerDelegate> urlHandlersByType =
            new Dictionary<Type, ControllerHandlerDelegate>()
            {
            };

        public Dictionary<string, ControllerHandlerDelegate> urlHandlersByTypeName =
            new Dictionary<string, ControllerHandlerDelegate>()
            {
            };

        #region Casts
        
        public object CastResourceProperty(object value, Type propertyType)
        {
            return this.CastResourceProperty(value, propertyType,
                (v) => v,
                () => throw new Exception());
        }

        public TResult CastResourceProperty<TResult>(object value, Type propertyType,
            Func<object, TResult> onCasted,
            Func<TResult> onNotMapped = default(Func<TResult>))
        {
            var valueType = value.GetType();
            if (propertyType.IsAssignableFrom(valueType))
                return onCasted(value);

            if (propertyType.IsAssignableFrom(typeof(BlackBarLabs.Api.Resources.WebId)))
            {
                if (value is Guid)
                {
                    var guidValue = (Guid)value;
                    var webIdValue = (BlackBarLabs.Api.Resources.WebId)guidValue;
                    return onCasted(webIdValue);
                }
            }

            if (typeof(IReferenceable).IsAssignableFrom(propertyType))
            {
                if (value is IReferenceable)
                {
                    var refValue = value as IReferenceable;
                    var guidIdValue = refValue.id;
                    return onCasted(guidIdValue);
                }
                if (value is Guid)
                {
                    var guidIdValue = (Guid)value;
                    return onCasted(guidIdValue);
                }
            }

            if (typeof(IReferenceableOptional).IsAssignableFrom(propertyType))
            {
                if (value is IReferenceableOptional)
                {
                    var refValue = value as IReferenceableOptional;
                    var guidIdMaybeValue = refValue.id;
                    return onCasted(guidIdMaybeValue);
                }
                if (value is Guid?)
                {
                    var guidIdMaybeValue = (Guid?)value;
                    return onCasted(guidIdMaybeValue);
                }
            }

            if (propertyType.IsAssignableFrom(typeof(string)))
            {
                if (value is Guid)
                {
                    var guidValue = (Guid)value;
                    var stringValue = guidValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is IReferenceable)
                {
                    var refValue = value as IReferenceable;
                    var guidIdValue = refValue.id;
                    var stringValue = guidIdValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is IReferenceableOptional)
                {
                    var refValue = value as IReferenceableOptional;
                    var guidIdMaybeValue = refValue.id;
                    if (!guidIdMaybeValue.HasValue)
                        return onCasted("null");

                    var stringValue = guidIdMaybeValue.Value.ToString();
                    return onCasted(stringValue);
                }
                if (value is BlackBarLabs.Api.Resources.WebId)
                {
                    var webIdValue = value as BlackBarLabs.Api.Resources.WebId;
                    var guidValue = webIdValue.ToGuid().Value;
                    var stringValue = guidValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is bool)
                {
                    var boolValue = (bool)value;
                    var stringValue = boolValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is DateTime)
                {
                    var dateValue = (DateTime)value;
                    var stringValue = dateValue.ToString();
                    return onCasted(stringValue);
                }
            }

            if (onNotMapped.IsDefaultOrNull())
                throw new Exception($"Cannot create {propertyType.FullName} from {value.GetType().FullName}");
            return onNotMapped();
        }


        #endregion

        #endregion

        #region Load Controllers

        private static IDictionary<string, IDictionary<HttpMethod, MethodInfo[]>> lookup;
        private static IDictionary<Type, string> contentTypeLookup;
        private static IDictionary<string, Type> resourceNameControllerLookup;
        private static IDictionary<Type, Type> resourceTypeControllerLookup;
        private static object lookupLock = new object();

        private void LocateControllers()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (!assembly.GlobalAssemblyCache))
                .ToArray();

            lock (lookupLock)
            {
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

        IDictionary<Type, HttpMethod> methodLookup =
            new Dictionary<Type, HttpMethod>()
            {
                { typeof(EastFive.Api.HttpGetAttribute), HttpMethod.Get },
                { typeof(EastFive.Api.HttpDeleteAttribute), HttpMethod.Delete },
                { typeof(EastFive.Api.HttpPostAttribute), HttpMethod.Post },
                { typeof(EastFive.Api.HttpPutAttribute), HttpMethod.Put },
                { typeof(EastFive.Api.HttpPatchAttribute), new HttpMethod("Patch") },
                { typeof(EastFive.Api.HttpOptionsAttribute), HttpMethod.Options },
                { typeof(EastFive.Api.HttpActionAttribute), new HttpMethod("actions") },
            };

        private void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly
                    .GetTypes();
                var functionViewControllerAttributesAndTypes = types
                    .Where(type => type.ContainsCustomAttribute<FunctionViewControllerAttribute>())
                    .Select(
                        (type) =>
                        {
                            var attr = type.GetCustomAttribute<FunctionViewControllerAttribute>();
                            return type.PairWithKey(attr);
                        })
                    .ToArray();
                
                lookup = lookup.Merge(
                    functionViewControllerAttributesAndTypes
                        .Select(
                            attrType =>
                            {
                                var actionMethods = attrType.Value
                                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .Where(method => method.ContainsCustomAttribute<HttpActionAttribute>())
                                    .GroupBy(method => method.GetCustomAttribute<HttpActionAttribute>().Method)
                                    .Select(methodGrp => (new HttpMethod(methodGrp.Key)).PairWithValue(methodGrp.ToArray()));

                                IDictionary<HttpMethod, MethodInfo[]> methods = methodLookup
                                        .Select(
                                            methodKvp => methodKvp.Value.PairWithValue(
                                                attrType.Value
                                                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                    .Where(method => method.ContainsCustomAttribute(methodKvp.Key))
                                            .ToArray()))
                                        .Concat(actionMethods)
                                        .ToDictionary();
                                
                                return attrType.Key.Route
                                    .IfThen(attrType.Key.Route.IsNullOrWhiteSpace(),
                                        (route) => attrType.Value.Name)
                                    .ToLower()
                                    .PairWithValue(methods);
                            }),
                        (k, v1, v2) => v2);

                resourceNameControllerLookup = resourceNameControllerLookup.Merge(
                    functionViewControllerAttributesAndTypes
                        .Select(
                            attrType =>
                            {
                                return attrType.Key.ContentType
                                    .IfThen(attrType.Key.ContentType.IsNullOrWhiteSpace(),
                                        (route) =>
                                        {
                                            var routeType = attrType.Key.Resource.IsDefaultOrNull()?
                                                attrType.Value
                                                :
                                                attrType.Key.Resource;
                                            return $"x-application/{routeType.Name}";
                                        })
                                    .ToLower()
                                    .PairWithValue(attrType.Value);
                            }),
                        (k, v1, v2) => v2);

                resourceTypeControllerLookup = functionViewControllerAttributesAndTypes
                    .FlatMap(
                        (attrType, next, skip) =>
                        {
                            if (attrType.Key.Resource.IsDefaultOrNull())
                                return skip();
                            return next(attrType.Key.Resource.PairWithValue(attrType.Value));
                        },
                        (IEnumerable<KeyValuePair<Type, Type>> kvps) =>
                            resourceTypeControllerLookup.Merge(
                                kvps,
                                (k, v1, v2) => v2).ToDictionary());

                contentTypeLookup = functionViewControllerAttributesAndTypes
                    .FlatMap(
                        (attrType, next, skip) =>
                        {
                            if (attrType.Key.ContentType.IsNullOrWhiteSpace())
                                return skip();
                            if (attrType.Key.Resource.IsDefaultOrNull())
                                return skip();
                            return next(attrType.Key.ContentType.PairWithKey(attrType.Key.Resource));
                        },
                        (IEnumerable<KeyValuePair<Type, string>> kvps) =>
                            contentTypeLookup.Merge(
                                kvps,
                                (k, v1, v2) => v2).ToDictionary());
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                ex.GetType();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

        #endregion

        #region Instigators

        #region Instigators - GENERIC

        public delegate Task<HttpResponseMessage> InstigatorDelegateGeneric(
                Type type, HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);

        public Dictionary<Type, InstigatorDelegateGeneric> instigatorsGeneric =
            new Dictionary<Type, InstigatorDelegateGeneric>()
            {
                {
                    typeof(Controllers.ReferencedDocumentDoesNotExistsResponse<>),
                    (type, httpApp, request, paramInfo, success) =>
                    {
                        var refDocMethodInfo = typeof(HttpApplication).GetMethod("RefDocDoesNotExist", BindingFlags.Public | BindingFlags.Static);
                        var dele = Delegate.CreateDelegate(type, request, refDocMethodInfo);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.ReferencedDocumentNotFoundResponse<>),
                    (type, httpApp, request, paramInfo, success) =>
                    {
                        var refDocMethodInfo = typeof(HttpApplication).GetMethod("ReferencedDocumentNotFound", BindingFlags.Public | BindingFlags.Static);
                        var dele = Delegate.CreateDelegate(type, request, refDocMethodInfo);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.MultipartResponseAsync<>),
                    (type, httpApp, request, paramInfo, success) =>
                    {
                        var scope = new GenericInstigatorScoping(type, httpApp, request, paramInfo);
                        var multipartResponseMethodInfoGeneric = typeof(GenericInstigatorScoping).GetMethod("MultipartResponseAsync", BindingFlags.Public | BindingFlags.Instance);
                        var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric.MakeGenericMethod(type.GenericTypeArguments);
                        var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                        return success((object)dele);
                    }
                }
            };

        public static HttpResponseMessage RefDocDoesNotExist(HttpRequestMessage request)
        {
            return request
                .CreateResponse(System.Net.HttpStatusCode.BadRequest)
                .AddReason("The query parameter did not reference an existing document.");
        }

        public static HttpResponseMessage ReferencedDocumentNotFound(HttpRequestMessage request)
        {
            return request
                .CreateResponse(System.Net.HttpStatusCode.NotFound)
                .AddReason("The document referrenced by the parameter was not found.");
        }

        private class GenericInstigatorScoping
        {
            private Type type;
            private HttpApplication httpApp;
            private HttpRequestMessage request;
            private ParameterInfo paramInfo;

            public GenericInstigatorScoping(Type type, HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo)
            {
                this.type = type;
                this.httpApp = httpApp;
                this.request = request;
                this.paramInfo = paramInfo;
            }

            public async Task<HttpResponseMessage> MultipartResponseAsync<T>(IEnumerableAsync<T> objectsAsync)
            {
                if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
                {
                    var objects = await objectsAsync.ToArrayAsync();
                    return await request.CreateMultisheetXlsxResponse(
                        new Dictionary<string, string>(),
                        objects.Cast<ResourceBase>()).ToTask();
                }

                var responses = await objectsAsync
                    .Select(
                        obj =>
                        {
                            var objType = obj.GetType();
                            if (!objType.ContainsAttributeInterface<IProvideSerialization>())
                            {
                                var response = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                                return response;
                            }

                            var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
                            var customResponse = serializationProvider.Serialize(httpApp, request, paramInfo, obj);
                            return customResponse;
                        })
                    .Async();

                if (request.Headers.Accept.Contains(accept => !accept.MediaType.ToLower().Contains("multipart")))
                {
                    var jsonStrings = await responses
                        .Select(v => v.Content.ReadAsStringAsync())
                        .AsyncEnumerable()
                        .ToArrayAsync();
                    var jsonArrayContent = $"[{jsonStrings.Join(",")}]";
                    var response = request.CreateResponse(HttpStatusCode.OK);
                    response.Content = new StringContent(jsonArrayContent, Encoding.UTF8, "application/json");
                    return response;
                }

                return await request.CreateMultipartResponseAsync(responses);
            }
        }

        #endregion

        public delegate Task<HttpResponseMessage> InstigatorDelegate(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);

        protected Dictionary<Type, InstigatorDelegate> instigators =
            new Dictionary<Type, InstigatorDelegate>()
            {
                {
                    typeof(EastFive.Api.Controllers.Security),
                    (httpApp, request, paramInfo, success) => request.GetActorIdClaimsAsync(
                        (actorId, claims) => success(
                            new Controllers.Security
                            {
                                performingAsActorId = actorId,
                                claims = claims,
                            }))
                },
                {
                    typeof(Controllers.ApiSecurity),
                    (httpApp, request, paramInfo, success) =>
                    {
                        return EastFive.Web.Configuration.Settings.GetString(AppSettings.ApiKey,
                            (authorizedApiKey) =>
                            {
                                var queryParams = request.RequestUri.ParseQueryString();
                                if (queryParams["ApiKeySecurity"] == authorizedApiKey)
                                    return success(new Controllers.ApiSecurity());

                                if (request.Headers.IsDefaultOrNull())
                                    return request.CreateResponse(HttpStatusCode.Unauthorized).ToTask();
                                if(request.Headers.Authorization.IsDefaultOrNull())
                                    return request.CreateResponse(HttpStatusCode.Unauthorized).ToTask();

                                if(request.Headers.Authorization.Parameter == authorizedApiKey)
                                    return success(new Controllers.ApiSecurity());
                                if(request.Headers.Authorization.Scheme == authorizedApiKey)
                                    return success(new Controllers.ApiSecurity());

                                return request.CreateResponse(HttpStatusCode.Unauthorized).ToTask();
                            },
                            (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).ToTask());
                    }
                },
                {
                    typeof(Controllers.SessionToken),
                    (httpApp, request, paramInfo, success) =>
                    {
                        return request.GetSessionIdClaimsAsync(
                            (sessionId, claims) =>
                            {
                                var x = new Controllers.SessionToken
                                {
                                    sessionId = sessionId,
                                    claims = claims,
                                };
                                return success(x);
                            });
                    }
                },
                {
                    typeof(System.Web.Http.Routing.UrlHelper),
                    (httpApp, request, paramInfo, success) => success(
                        new System.Web.Http.Routing.UrlHelper(request))
                },
                {
                    typeof(HttpRequestMessage),
                    (httpApp, request, paramInfo, success) => success(request)
                },
                {
                    typeof(Controllers.GeneralConflictResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.GeneralConflictResponse dele = (why) => request.CreateResponse(System.Net.HttpStatusCode.Conflict).AddReason(why);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.GeneralFailureResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.GeneralFailureResponse dele = (why) => request.CreateResponse(System.Net.HttpStatusCode.InternalServerError).AddReason(why);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.ServiceUnavailableResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.ServiceUnavailableResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.ServiceUnavailable);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.ConfigurationFailureResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.ConfigurationFailureResponse dele = 
                            (configurationValue, message) => request
                                .CreateResponse(System.Net.HttpStatusCode.ServiceUnavailable)
                                .AddReason($"`{configurationValue}` not specifiedin config:{message}");
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.BadRequestResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.BadRequestResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.BadRequest).AddReason("Bad request.");
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.AlreadyExistsResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.AlreadyExistsResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.Conflict).AddReason("Resource has already been created");
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.AlreadyExistsReferencedResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.AlreadyExistsReferencedResponse dele = (existingId) => request.CreateResponse(System.Net.HttpStatusCode.Conflict).AddReason("The resource already exists");
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.NoContentResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.NoContentResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.NoContent);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.NotFoundResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.NotFoundResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.NotFound);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.ContentResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.ContentResponse dele = 
                            (obj, contentType) =>
                            {
                                var objType = obj.GetType();
                                if(!objType.ContainsAttributeInterface<IProvideSerialization>())
                                {
                                    var response = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                                    if(!contentType.IsNullOrWhiteSpace())
                                        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                                    return response;
                                }

                                var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
                                var customResponse = serializationProvider.Serialize(httpApp, request, paramInfo, obj);
                                return customResponse;
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.HtmlResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.HtmlResponse dele = (html) =>
                        {
                            var response = request.CreateHtmlResponse(html);
                            return response;
                        };
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.XlsxResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.XlsxResponse dele = (xlsxData, filename) =>
                        {
                            var response = request.CreateXlsxResponse(xlsxData, filename);
                            return response;
                        };
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.MultipartResponseAsync),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.MultipartResponseAsync dele = (responses) => request.CreateMultipartResponseAsync(responses);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.RedirectResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.RedirectResponse dele = (redirectLocation, why) => request.CreateRedirectResponse(redirectLocation).AddReason(why);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.MultipartAcceptArrayResponseAsync),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.MultipartAcceptArrayResponseAsync dele =
                            (objects) =>
                            {
                                if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
                                {
                                    return request.CreateMultisheetXlsxResponse(
                                        new Dictionary<string, string>(),
                                        objects.Cast<ResourceBase>()).ToTask();
                                }
                                var responses = objects.Select(obj => request.CreateResponse(System.Net.HttpStatusCode.OK, obj));
                                return request.CreateMultipartResponseAsync(responses);
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.BackgroundResponseAsync),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.BackgroundResponseAsync dele =
                            async (callback) =>
                            {
                                if(request.Headers.Accept.Contains(mediaType => mediaType.MediaType.ToLower().Contains("background")))
                                {
                                    var urlHelper = request.GetUrlHelper();
                                    var processId = Controllers.BackgroundProgressController.CreateProcess(callback, 1.0);
                                    var response = request.CreateResponse(HttpStatusCode.Accepted);
                                    response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
                                    response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<Controllers.BackgroundProgressController>(processId).AbsoluteUri);
                                    return response;
                                }
                                return await callback(v => { }); // TODO: Not this
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.ReferencedDocumentNotFoundResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.ReferencedDocumentNotFoundResponse dele = () => request
                            .CreateResponse(System.Net.HttpStatusCode.BadRequest)
                            .AddReason("The query parameter did not reference an existing document.");
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.UnauthorizedResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.UnauthorizedResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.ForbiddenResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.ForbiddenResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.AcceptedResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.AcceptedResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.Accepted);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.NotModifiedResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.NotModifiedResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.NotModified);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.NotImplementedResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.NotImplementedResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.NotImplemented);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.CreatedResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.CreatedResponse dele = () => request.CreateResponse(System.Net.HttpStatusCode.Created);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.CreatedBodyResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.CreatedBodyResponse dele =
                            (obj, contentType) =>
                            {
                                var response = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                                if(!contentType.IsNullOrWhiteSpace())
                                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                                return response;
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.AcceptedBodyResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.AcceptedBodyResponse dele =
                            (obj, contentType) =>
                            {
                                var response = request.CreateResponse(System.Net.HttpStatusCode.Accepted, obj);
                                if(!contentType.IsNullOrWhiteSpace())
                                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                                return response;
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(EastFive.Api.Controllers.ViewFileResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        EastFive.Api.Controllers.ViewFileResponse dele =
                            (filePath, content) =>
                            {
                                try
                                {
                                    var parsedView =  RazorEngine.Engine.Razor.RunCompile(filePath, null, content);
                                    return request.CreateHtmlResponse(parsedView);
                                }
                                catch(RazorEngine.Templating.TemplateCompilationException ex)
                                {
                                    var body = ex.CompilerErrors.Select(error => error.ErrorText).Join(";\n\n");
                                    return request.CreateHtmlResponse(body);
                                } catch(Exception ex)
                                {
                                    var body = $"Could not load template {filePath} due to:[{ex.GetType().FullName}] `{ex.Message}`";
                                    return request.CreateHtmlResponse(body);
                                }
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(EastFive.Api.Controllers.ViewStringResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        EastFive.Api.Controllers.ViewStringResponse dele =
                            (razorTemplate, content) =>
                            {
                                var response = request.CreateResponse(HttpStatusCode.OK);
                                var parsedView =  RazorEngine.Razor.Parse(razorTemplate, content);
                                response.Content = new StringContent(parsedView);
                                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
                                return response;
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(EastFive.Api.Controllers.ViewRenderer),
                    (httpApp, request, paramInfo, success) =>
                    {
                        EastFive.Api.Controllers.ViewRenderer dele =
                            (filePath, content) =>
                            {
                                try
                                {
                                    var parsedView =  RazorEngine.Engine.Razor.RunCompile(filePath, null, content);
                                    return parsedView;
                                }
                                catch(RazorEngine.Templating.TemplateCompilationException ex)
                                {
                                    var body = ex.CompilerErrors.Select(error => error.ErrorText).Join(";\n\n");
                                    return body;
                                } catch(Exception ex)
                                {
                                    return $"Could not load template {filePath} due to:[{ex.GetType().FullName}] `{ex.Message}`";
                                }
                            };
                        return success((object)dele);
                    }
                },
            };
        
        public void AddInstigator(Type type, InstigatorDelegate instigator)
        {
            instigators.Add(type, instigator);
        }

        public void SetInstigator(Type type, InstigatorDelegate instigator)
        {
            instigators[type] = instigator;
        }

        public void AddGenericInstigator(Type type, InstigatorDelegateGeneric instigator)
        {
            instigatorsGeneric.Add(type, instigator);
        }

        public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator)
        {
            instigatorsGeneric[type] = instigator;
        }

        internal Task<HttpResponseMessage> Instigate(HttpRequestMessage request, ParameterInfo methodParameter,
            Func<object, Task<HttpResponseMessage>> onInstigated)
        {

            if (this.instigators.ContainsKey(methodParameter.ParameterType))
                return this.instigators[methodParameter.ParameterType](this, request, methodParameter,
                    (v) => onInstigated(v));

            if (methodParameter.ParameterType.IsGenericType)
            {
                var possibleGenericInstigator = this.instigatorsGeneric
                    .Where(instigatorKvp => instigatorKvp.Key.GUID == methodParameter.ParameterType.GUID)
                    .ToArray();
                if (possibleGenericInstigator.Any())
                    return possibleGenericInstigator.First().Value(methodParameter.ParameterType,
                        this, request, methodParameter,
                    (v) => onInstigated(v));
            }

            if (methodParameter.ParameterType.IsInstanceOfType(this))
                return onInstigated(this);

            return request.CreateResponse(HttpStatusCode.InternalServerError)
                .AddReason($"Could not instigate type: {methodParameter.ParameterType.FullName}. Please add an instigator for that type.")
                .AsTask();
        }

        #endregion

        #region Bindings

        public delegate object BindingDelegate(HttpApplication httpApp, IParseToken content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        protected Dictionary<Type, BindingDelegate> bindings =
            new Dictionary<Type, BindingDelegate>()
            {
                {
                    typeof(string),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var stringValue = content.ReadString();
                        return onParsed((object)stringValue);
                    }
                },
                {
                    typeof(Guid),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if(content.IsString)
                        {
                            var guidStringValue = content.ReadString();
                            if (Guid.TryParse(guidStringValue, out Guid stringGuidValue))
                                return onParsed(stringGuidValue);
                            return onNotConvertable($"Failed to convert `{guidStringValue}` to type `{typeof(Guid).FullName}`.");
                        }
                        var webId = content.ReadObject<WebId>();
                        var guidValueMaybe = webId.ToGuid();
                        if(!guidValueMaybe.HasValue)
                            return onNotConvertable("Null WebId cannot be converted to a Guid.");
                        var webIdGuidValue = guidValueMaybe.Value;
                        return onParsed(webIdGuidValue);
                    }
                },
                {
                    typeof(Guid[]),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var tokens = content.ReadArray();
                        var guids = tokens
                            .Select(
                                token => httpApp.Bind(typeof(Guid), token,
                                    guid => guid,
                                    (why) => default(Guid)))
                            .Cast<Guid>()
                            .ToArray();
                        return onParsed(guids);
                    }
                },
                {
                    typeof(DateTime),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var dateStringValue = token.ReadString();
                        if (DateTime.TryParse(dateStringValue, out DateTime dateValue))
                            return onParsed(dateValue);
                        return onNotConvertable($"Failed to convert {dateStringValue} to `{typeof(DateTime).FullName}`.");
                    }
                },
                {
                    typeof(bool),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var boolStringValue = token.ReadString();
                        if ("t" == boolStringValue.ToLower())
                            return onParsed(true);

                        if ("on" == boolStringValue.ToLower()) // used in check boxes
                            return onParsed(true);

                        if ("f" == boolStringValue)
                            return onParsed(false);

                        if ("off" == boolStringValue.ToLower()) // used in some check boxes
                            return onParsed(false);

                        // TryParse may convert "on" to false TODO: Test theory
                        if (bool.TryParse(boolStringValue, out bool boolValue))
                            return onParsed(boolValue);

                        return onNotConvertable($"Failed to convert {boolStringValue} to `{typeof(bool).FullName}`.");
                    }
                },
                {
                    typeof(Type),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        return httpApp.GetResourceType(content.ReadString(),
                            (typeInstance) => onParsed(typeInstance),
                            () => content.ReadString().GetClrType(
                                typeInstance => onParsed(typeInstance),
                                () => onNotConvertable(
                                    $"`{content}` is not a recognizable resource type or CLR type.")));
                    }
                },
                {
                    typeof(Stream),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        try
                        {
                            var byteArrayBase64 = content.ReadString();
                            var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                            return onParsed(new MemoryStream(byteArrayValue));
                        } catch(Exception ex)
                        {
                            return content.ReadStream();
                            //return onNotConvertable($"Failed to convert {content} to `{typeof(Stream).FullName}` as base64 string:{ex.Message}.");
                        }
                    }
                },
                {
                    typeof(byte[]),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        try
                        {
                            var byteArrayBase64 = content.ReadString();
                            var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                            return onParsed(byteArrayValue);
                        } catch(Exception ex)
                        {
                            return content.ReadBytes();
                            //return onNotConvertable($"Failed to convert {content} to `{typeof(byte[]).FullName}` as base64 string:{ex.Message}.");
                        }
                    }
                },
                {
                    typeof(WebId),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        try
                        {
                            if(!Guid.TryParse(content.ReadString(), out Guid guidValue))
                                return onNotConvertable($"Could not convert `{content}` to GUID");
                            var webIdObj = (object) new WebId() { UUID = guidValue };
                            return onParsed(webIdObj);
                        } catch (Exception ex)
                        {
                            var result = content.ReadObject<WebId>();
                            return onParsed(result);
                        }
                    }
                },
                {
                    typeof(Controllers.WebIdAny),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if (String.Compare(content.ReadString().ToLower(), "any") == 0)
                            return onParsed(new Controllers.WebIdAny());
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Controllers.WebIdAny).FullName}`.");
                    }
                },
                {
                    typeof(Controllers.WebIdNone),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if (String.Compare(content.ReadString().ToLower(), "none") == 0)
                            return onParsed(new Controllers.WebIdNone());
                        return onNotConvertable($"`{content}` is not a web ID of none. Please use format `none` to specify no web ID provided.");
                    }
                },
                {
                    typeof(Controllers.WebIdNot),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var contentStr = content.ReadString();
                        return contentStr.ToUpper().MatchRegexInvoke("NOT\\((?<notString>[a-zA-Z]+)\\)",
                            (string notString) => notString,
                            (string[] notStrings) =>
                            {
                                if (!notStrings.Any())
                                    return onNotConvertable($"`{contentStr}` is not parsable as an exlusion list. Please use format `NOT(ABC123-....-EDF1)`");
                                var notString = notStrings.First();
                                if (!Guid.TryParse(notString, out Guid notUUID))
                                    return onNotConvertable($"`{notString}` is not a UUID. Please use format `NOT(ABC123-....-EDF1)`");
                                return onParsed(
                                    new Controllers.WebIdNot()
                                    {
                                        notUUID = notUUID,
                                    });
                            });
                    }
                },
                {
                    typeof(Controllers.DateTimeEmpty),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if (String.Compare(content.ReadString().ToLower(), "false") == 0)
                            return onParsed(new Controllers.DateTimeEmpty());
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Controllers.DateTimeEmpty).FullName}`.");
                    }
                },
                {
                    typeof(Controllers.DateTimeQuery),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if(DateTime.TryParse(content.ReadString(), out DateTime startEnd))
                            return onParsed(new Controllers.DateTimeQuery(startEnd, startEnd));
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Controllers.DateTimeQuery).FullName}`.");
                    }
                },
                {
                    typeof(object),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        return onParsed(content.ReadString());
                    }
                }
            };

        public delegate object BindingGenericDelegate(Type type, HttpApplication httpApp, IParseToken content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        protected Dictionary<Type, BindingGenericDelegate> bindingsGeneric =
            new Dictionary<Type, BindingGenericDelegate>()
            {
                {
                    typeof(IRef<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        var referredType = type.GenericTypeArguments.First();
                        var refType = referredType.IsClass?
                            typeof(EastFive.RefObj<>).MakeGenericType(referredType)
                            :
                            typeof(EastFive.Ref<>).MakeGenericType(referredType);
                        
                        var defaultObj = referredType.GetDefault();
                        var asTaskMethodGeneric = typeof(Extensions.ObjectExtensions).GetMethod("AsTask", BindingFlags.Static | BindingFlags.Public);
                        var askTaskMethod = asTaskMethodGeneric.MakeGenericMethod(new [] { referredType });
                        var defaultObjTask = askTaskMethod.Invoke(null, new object [] {defaultObj });

                        var refInstance = Activator.CreateInstance(refType,
                            new object [] { defaultObjTask });
                        return onBound(refInstance);
                    }
                },
                {
                    typeof(IRefOptional<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        var referredType = type.GenericTypeArguments.First();
                        if(referredType.IsClass)
                        {
                            var refObjType = typeof(IRefObj<>).MakeGenericType(referredType);
                            var refObjOptionalType = typeof(RefObjOptional<>).MakeGenericType(referredType);
                            var refObjInstance = httpApp.Bind(refObjType, content,
                                (v) =>
                                {
                                    var refInst = Activator.CreateInstance(refObjOptionalType, new object [] { v });
                                    return refInst;
                                },
                                (why) =>
                                {
                                    var refInst = Activator.CreateInstance(refObjOptionalType, new object [] { });
                                    return refInst;
                                });
                            return onBound(refObjInstance);
                        }
                        var refType = typeof(IRef<>).MakeGenericType(referredType);
                        var refOptionalType = typeof(RefOptional<>).MakeGenericType(referredType);

                        Func<object> emptyOptional =
                            () =>
                            {
                                var refInst = Activator.CreateInstance(refOptionalType, new object [] { });
                                return onBound(refInst);
                            };

                        object ParseContent(IParseToken parseToken)
                        {
                            return httpApp.Bind(refType, parseToken,
                                (v) =>
                                {
                                    var refInst = Activator.CreateInstance(refOptionalType, new object [] { v });
                                    return onBound(refInst);
                                },
                                (why) => emptyOptional());
                        }

                        // Check for null/empty
                        if(!content.IsString)
                            return ParseContent(content);

                        var stringValue = content.ReadString();
                        if (stringValue.IsNullOrWhiteSpace())
                            return emptyOptional();
                        if (stringValue.ToLower() == "empty")
                            return emptyOptional();
                        if (stringValue.ToLower() == "null")
                            return emptyOptional();

                        var stringContent = new ParseToken(stringValue);
                        return ParseContent(stringContent);
                    }
                },
                {
                    typeof(Nullable<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        var nullableType = type.GenericTypeArguments.First();
                        var refInstance = httpApp.Bind(nullableType, content,
                            (v) =>
                            {
                                var nonNullable = v.AsNullable();
                                return nonNullable;
                            },
                            (why) => type.GetDefault());
                        return onBound(refInstance);
                    }
                },
                {
                    typeof(IDictionary<,>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        var keyType = type.GenericTypeArguments[0];
                        var valueType = type.GenericTypeArguments[1];
                        var refType = typeof(Dictionary<,>).MakeGenericType(type.GenericTypeArguments);
                        var refInstance = Activator.CreateInstance(refType);
                        var addMethod = refType.GetMethod("Add");
                        //Dictionary<string, int> dict;
                        //dict.Add()
                        foreach(var kvpToken in content.ReadDictionary())
                        {
                            var keyToken = new FormDataTokenParser(kvpToken.Key);
                            var valueToken = kvpToken.Value;
                            var result = httpApp.Bind(keyType, keyToken,
                                keyValue =>
                                {
                                    return httpApp.Bind(valueType, valueToken,
                                        valueValue =>
                                        {
                                            addMethod.Invoke(refInstance,
                                                new object [] { keyValue, valueValue });
                                            return string.Empty;
                                        },
                                        (why) => why);
                                },
                                (why) => why);
                        }

                        return onBound(refInstance);
                    }
                }
            };

        internal bool CanBind(Type type)
        {
            if (this.bindings.ContainsKey(type))
                return true;

            if (type.IsGenericType)
            {
                var possibleGenericInstigator = this.bindingsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                return possibleGenericInstigator.Any();
            }

            return false;
        }

        public TResult Bind<TResult>(Type type, IParseToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            if (this.bindings.ContainsKey(type))
                return (TResult)this.bindings[type](this, content,
                    (v) => onParsed(v),
                    (why) => onDidNotBind(why));

            if (type.IsGenericType)
            {
                var possibleGenericInstigator = this.bindingsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                if (possibleGenericInstigator.Any())
                {
                    var genericInstigator = possibleGenericInstigator.First().Value;
                    var resultBound = genericInstigator(type,
                            this, content,
                        (v) =>
                        {
                            var result = onParsed(v);
                            return result;
                        },
                        (why) =>
                        {
                            return onDidNotBind(why);
                        });
                    var castResult = (TResult)resultBound;
                    return castResult;
                }
            }
            
            return onDidNotBind($"No binding for type `{type.FullName}` active in server.");
        }

        public void AddOrUpdateBinding(Type type, BindingDelegate binding)
        {
            if (bindings.ContainsKey(type))
                bindings[type] = binding;
            else
                bindings.Add(type, binding);
        }

        public void AddOrUpdateGenericBinding(Type type, BindingGenericDelegate binding)
        {
            if (this.bindingsGeneric.ContainsKey(type))
                this.bindingsGeneric[type] = binding;
            else
                this.bindingsGeneric.Add(type, binding);
        }

        #endregion

        #region Instantiations
        
        public delegate Task<object> InstantiationDelegate(HttpApplication httpApp);

        protected Dictionary<Type, InstantiationDelegate> instantiations =
            new Dictionary<Type, InstantiationDelegate>()
            {
                {
                    typeof(EastFive.Api.Controllers.ViewPathResolver),
                    (httpApp) =>
                    {
                        EastFive.Api.Controllers.ViewPathResolver dele =
                            (viewPath) =>
                            {
                                return $"{HttpRuntime.AppDomainAppPath}Views\\{viewPath}";
                            };
                        return dele.AsTask<object>();
                    }
                },
            };

        public delegate Task<object> InstantiationGenericDelegate(Type type, HttpApplication httpApp);

        protected Dictionary<Type, InstantiationGenericDelegate> instantiationsGeneric =
            new Dictionary<Type, InstantiationGenericDelegate>()
            {
                {
                    typeof(IRef<>),
                    (type, httpApp) =>
                    {
                        var referredType = type.GenericTypeArguments.First();
                        var refType = referredType.IsClass?
                            typeof(EastFive.RefObj<>).MakeGenericType(referredType)
                            :
                            typeof(EastFive.Ref<>).MakeGenericType(referredType);
                        var refInstance = Activator.CreateInstance(refType,
                            new object [] { referredType.GetDefault().AsTask() });
                        return refInstance.AsTask();
                    }
                },
            };

        internal bool CanInstantiate(Type type)
        {
            if (this.instantiations.ContainsKey(type))
                return true;

            if (type.IsGenericType)
            {
                var possibleGenericInstantiator = this.instantiationsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                return possibleGenericInstantiator.Any();
            }

            return false;
        }

        public async Task<TResult> InstantiateAsync<TResult>(Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            if (this.instantiations.ContainsKey(type))
            {
                var instance = await this.instantiations[type](this);
                var castedVal = onParsed(instance);
                return castedVal;
            }

            if (type.IsGenericType)
            {
                var possibleGenericInstantiator = this.instantiationsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                if (possibleGenericInstantiator.Any())
                {
                    var resultBound = await possibleGenericInstantiator.First().Value(type, this);
                    var castResult = (TResult)resultBound;
                    return castResult;
                }
            }

            return onDidNotBind($"No binding for type `{type.FullName}` active in server.");
        }

        public IEnumerableAsync<T> InstantiateAll<T>()
        {
            var type = typeof(T);
            return this.instantiations
                .Where(
                    instigatorKvp =>
                    {
                        if (instigatorKvp.Key.GUID == type.GUID)
                            return true;
                        if (type.IsAssignableFrom(instigatorKvp.Key))
                            return true;
                        return false;
                    })
                .Select(
                    async instantiator =>
                    {
                        var resultBound = await instantiator.Value(this);
                        return resultBound;
                    })
                .Concat(
                    this.instantiationsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        })
                    .Select(
                        async instantiator =>
                        {
                            var resultBound = await instantiator.Value(type, this);
                            return resultBound;
                        }))
                .AsyncEnumerable()
                .Where(v => v is T)
                .Select(
                    instantiationResult =>
                    {
                        return (T)instantiationResult;
                    });
        }

        public void AddOrUpdateInstantiation(Type type, InstantiationDelegate instantiation)
        {
            if (instantiations.ContainsKey(type))
                instantiations[type] = instantiation;
            else
                instantiations.Add(type, instantiation);
        }

        public void AddOrUpdateGenericInstantiation(Type type, InstantiationGenericDelegate instantiation)
        {
            if (this.bindingsGeneric.ContainsKey(type))
                this.instantiationsGeneric[type] = instantiation;
            else
                this.instantiationsGeneric.Add(type, instantiation);
        }

        #endregion

        #region Conversions

        public class MemoryStreamForFile : MemoryStream
        {
            public MemoryStreamForFile(byte[] buffer) : base(buffer) { }
            public string FileName { get; set; }
        }

        public delegate Task<TResult> ParseContentDelegate<TResult>(string key, Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

        public JsonConverter GetExtrudeConverter(HttpRequestMessage request, UrlHelper urlHelper)
        {
            var useWebIds = request.Headers.Accept.Contains(
                header =>
                {
                    if (header.MediaType.ToLower() != "application/json")
                        return false;
                    var requestWebId = header.Parameters.Contains(
                        nhv =>
                        {
                            if (nhv.Name.ToLower() != "id")
                                return false;
                            if (nhv.Value.ToLower() == "webid")
                                return true;
                            return false;
                        });
                    return requestWebId;
                });
            return new ExtrudeConvert(this, urlHelper, useWebIds);
        }

        private class ExtrudeConvert : Newtonsoft.Json.JsonConverter
        {
            HttpApplication application;
            UrlHelper urlHelper;
            bool useWebIds;

            public ExtrudeConvert(HttpApplication httpApplication, UrlHelper urlHelper, bool useWebIds)
            {
                this.application = httpApplication;
                this.urlHelper = urlHelper;
                this.useWebIds = useWebIds;
            }

            public override bool CanConvert(Type objectType)
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                    return true;
                if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                    return true;
                if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                    return true;
                if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
                {
                    if (objectType.GetGenericArguments().Any(arg => CanConvert(arg)))
                        return true;
                }
                if (objectType.IsSubclassOf(typeof(Type)))
                    return true;
                return false;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException("Extruder does not read values");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                void WriteId(Guid? idMaybe)
                {
                    if (!idMaybe.HasValue)
                    {
                        writer.WriteValue((string)null);
                        return;
                    }

                    var id = idMaybe.Value;
                    if (!useWebIds)
                    {
                        writer.WriteValue(id);
                        return;
                    }

                    // The webID could be created and then serialized, etc. 
                    // or.... it can just be writen inline here.

                    writer.WriteStartObject();
                    var key = id.ToString();
                    writer.WritePropertyName("key");
                    writer.WriteValue(key);

                    var uuid = id;
                    writer.WritePropertyName("uuid");
                    writer.WriteValue(uuid);

                    var valueType = value.GetType();
                    if(!valueType.IsGenericType)
                    {
                        writer.WriteEndObject();
                        return;
                    }

                    // TODO: Handle dictionary, etc
                    var refType = valueType.GetGenericArguments().First();
                    var webId = urlHelper.GetWebId(refType, id);

                    var contentType = $"x-application/x-{refType.Name.ToLower()}";
                    if (refType.ContainsCustomAttribute<FunctionViewControllerAttribute>(true))
                    {
                        var fvcAttrContentType = refType.GetCustomAttribute<FunctionViewControllerAttribute>().ContentType;
                        if (fvcAttrContentType.HasBlackSpace())
                            contentType = fvcAttrContentType;
                    }
                    var applicationNamespace = application.Namespace;
                    var urnString = $"urn:{contentType}:{applicationNamespace}:{key}";
                    writer.WritePropertyName("urn");
                    writer.WriteValue(urnString);

                    var source = urlHelper.GetLocationWithId(refType, id);
                    writer.WritePropertyName("source");
                    writer.WriteValue(source.AbsoluteUri);
                    writer.WriteEndObject();

                    return;

                }
                if (value is IReferenceable)
                {
                    var id = (value as IReferenceable).id;
                    WriteId(id);
                    //writer.WriteValue(id);
                    return;
                }
                if (value is IReferences)
                {
                    writer.WriteStartArray();
                    Guid[] ids = (value as IReferences).ids
                        .Select(
                            id =>
                            {
                                WriteId(id);
                                //writer.WriteValue(id);
                                return id;
                            })
                        .ToArray();
                    writer.WriteEndArray();
                    return;
                }
                if (value is IReferenceableOptional)
                {
                    var id = (value as IReferenceableOptional).id;
                    WriteId(id);
                    //writer.WriteValue(id);
                    return;
                }
                if (value.GetType().IsSubClassOfGeneric(typeof(IDictionary<,>)))
                {
                    writer.WriteStartObject();
                    foreach (var kvpObj in value.DictionaryKeyValuePairs())
                    {
                        var keyValue = kvpObj.Key;
                        var propertyName = (keyValue is IReferenceable) ?
                            (keyValue as IReferenceable).id.ToString("N")
                            :
                            keyValue.ToString();
                        writer.WritePropertyName(propertyName);

                        var valueValue = kvpObj.Value;
                        writer.WriteValue(valueValue);
                    }
                    writer.WriteEndObject();
                    return;
                }
                if (value is Type)
                {
                    var stringType = (value as Type).GetClrString();
                    writer.WriteValue(stringType);
                    return;
                }
            }
        }

        private class BindConvert : Newtonsoft.Json.JsonConverter
        {
            HttpApplication application;

            private class JsonReaderTokenParser : IParseToken
            {
                private JsonReader reader;
                private HttpApplication application;

                public JsonReaderTokenParser(JsonReader reader, HttpApplication application)
                {
                    this.reader = reader;
                    this.application = application;
                }


                public bool IsString
                {
                    get
                    {
                        if (reader.TokenType == JsonToken.String)
                            return true;
                        //if (reader.TokenType == JsonToken.Boolean)
                        //    return true;
                        //if (reader.TokenType == JsonToken.Null)
                        //    return true;
                        return false;
                    }
                }

                public string ReadString()
                {
                    if (reader.TokenType == JsonToken.Boolean)
                    {
                        var valueBool = (bool)reader.Value;
                        var value = valueBool.ToString();
                        return value;
                    }
                    if (reader.TokenType == JsonToken.String)
                    {

                        var value = (string)reader.Value;
                        return value;
                    }
                    if (reader.TokenType == JsonToken.Null)
                    {
                        return string.Empty;
                    }
                    if (reader.TokenType == JsonToken.StartArray)
                    {
                        return string.Empty;
                    }
                    throw new Exception($"BindConvert does not handle token type: {reader.TokenType}");
                }

                public T ReadObject<T>()
                {
                    var token = JToken.Load(reader);
                    if (token is JValue)
                        return token.Value<T>();

                    return token.ToObject<T>();
                }

                public IParseToken[] ReadArray()
                {
                    var token = JToken.Load(reader);
                    var tokenParser = new JsonTokenParser(token);
                    return tokenParser.ReadArray();
                }

                public IDictionary<string, IParseToken> ReadDictionary()
                {
                    var token = JToken.Load(reader);
                    var tokenParser = new JsonTokenParser(token);
                    return tokenParser.ReadDictionary();
                }

                public byte[] ReadBytes()
                {
                    throw new NotImplementedException();
                }

                public Stream ReadStream()
                {
                    throw new NotImplementedException();
                }
            }

            public BindConvert(HttpApplication httpApplication)
            {
                this.application = httpApplication;
            }

            public override bool CanConvert(Type objectType)
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                    return true;
                if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                    return true;
                return this.application.CanBind(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return this.application.Bind(objectType, new JsonReaderTokenParser(reader, this.application),
                        v => v,
                        (why) => existingValue);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException("BindConvert cannot write values.");
            }
        }

        private class MultipartContentTokenParser : IParseToken
        {
            private byte[] contents;
            private string fileNameMaybe;

            public MultipartContentTokenParser(byte[] contents, string fileNameMaybe)
            {
                this.contents = contents;
                this.fileNameMaybe = fileNameMaybe;
            }

            public IParseToken[] ReadArray()
            {
                throw new NotImplementedException();
            }

            public byte[] ReadBytes()
            {
                return contents;
            }

            public IDictionary<string, IParseToken> ReadDictionary()
            {
                throw new NotImplementedException();
            }

            public T ReadObject<T>()
            {
                throw new NotImplementedException();
            }

            public Stream ReadStream()
            {
                return new MemoryStreamForFile(contents)
                { FileName = fileNameMaybe };
            }

            public bool IsString => true;
            public string ReadString()
            {
                return System.Text.Encoding.UTF8.GetString(contents);
            }
        }

        private class JsonTokenParser : IParseToken
        {
            private JToken valueToken;

            public JsonTokenParser(JToken valueToken)
            {
                this.valueToken = valueToken;
            }

            public byte[] ReadBytes()
            {
                return valueToken.ToObject<byte[]>();
            }

            public Stream ReadStream()
            {
                return valueToken.ToObject<Stream>();
            }

            public bool IsString
            {
                get
                {
                    return valueToken.Type == JTokenType.String;
                }
            }

            public string ReadString()
            {
                return valueToken.ToObject<string>();
            }
            
            public T ReadObject<T>()
            {
                if(valueToken is JObject)
                {
                    var jObj = valueToken as Newtonsoft.Json.Linq.JObject;
                    var jsonText = jObj.ToString();
                    var value = JsonConvert.DeserializeObject<T>(jsonText);
                    return value;
                }
                return valueToken.Value<T>();
            }

            public IParseToken[] ReadArray()
            {
                if(valueToken.Type == JTokenType.Array)
                    return (valueToken as JArray)
                        .Select(
                            token => new JsonTokenParser(token))
                        .ToArray();
                if(valueToken.Type == JTokenType.Null)
                    return new IParseToken[] { };
                if (valueToken.Type == JTokenType.Undefined)
                    return new IParseToken[] { };

                if (valueToken.Type == JTokenType.Object)
                    return valueToken
                        .Children()
                        .Select(childToken => new JsonTokenParser(childToken))
                        .ToArray();

                if (valueToken.Type == JTokenType.Property)
                {
                    var property = (valueToken as JProperty);
                    return new JsonTokenParser(property.Value).AsArray();
                }

                return valueToken
                    .Children()
                    .Select(child => new JsonTokenParser(child))
                    .ToArray();
            }

            public IDictionary<string, IParseToken> ReadDictionary()
            {
                KeyValuePair<string, IParseToken> ParseToken(JToken token)
                {
                    if (token.Type == JTokenType.Property)
                    {
                        var propertyToken = (token as JProperty);
                        return new JsonTokenParser(propertyToken.Value)
                            .PairWithKey<string, IParseToken>(propertyToken.Name);
                    }
                    return (new JsonTokenParser(token))
                        .PairWithKey<string, IParseToken>(token.ToString());
                }

                if (valueToken.Type == JTokenType.Array)
                    return (valueToken as JArray)
                        .Select(ParseToken)
                        .ToDictionary();
                if (valueToken.Type == JTokenType.Null)
                    return new Dictionary<string, IParseToken>();
                if (valueToken.Type == JTokenType.Undefined)
                    return new Dictionary<string, IParseToken>();

                if (valueToken.Type == JTokenType.Object)
                    return valueToken
                        .Children()
                        .Select(ParseToken)
                        .ToDictionary();

                return valueToken
                        .Children()
                        .Select(ParseToken)
                        .ToDictionary();
            }
        }

        private class FormDataTokenParser : IParseToken
        {
            private string valueForKey;

            public FormDataTokenParser(string valueForKey)
            {
                this.valueForKey = valueForKey;
            }

            public IParseToken[] ReadArray()
            {
                throw new NotImplementedException();
            }

            public byte[] ReadBytes()
            {
                throw new NotImplementedException();
            }

            public IDictionary<string, IParseToken> ReadDictionary()
            {
                throw new NotImplementedException();
            }

            public T ReadObject<T>()
            {
                throw new NotImplementedException();
            }

            public Stream ReadStream()
            {
                throw new NotImplementedException();
            }

            public bool IsString => true;
            
            public string ReadString()
            {
                return this.valueForKey;
            }
        }

        public virtual async Task<TResult> ParseContentValuesAsync<TParseResult, TResult>(HttpContent content,
            Func<ParseContentDelegate<TParseResult>, string [], Task<TResult>> onParsedContentValues)
        {
            Task<TResult> InvalidContent(string errorMessage)
            {
                ParseContentDelegate<TParseResult> parser =
                    (key, type, onFound, onFailure) =>
                        onFailure(errorMessage).AsTask();
                return onParsedContentValues(parser, new string[] { });
            }

            if (content.IsDefaultOrNull())
                return await InvalidContent("Body content was not provided.");

            if (content.IsJson())
            {
                var contentString = await content.ReadAsStringAsync();
                var bindConvert = new BindConvert(this);
                JObject contentJObject;
                try
                {
                    contentJObject = Newtonsoft.Json.Linq.JObject.Parse(contentString);
                }
                catch (Exception ex)
                {
                    ParseContentDelegate<TParseResult> exceptionParser =
                        async (key, type, onFound, onFailure) =>
                        {
                            return onFailure(ex.Message);
                        };
                    var exceptionKeys = new string[] { };
                    return await onParsedContentValues(exceptionParser, exceptionKeys);
                }
                ParseContentDelegate<TParseResult> parser =
                    async (key, type, onFound, onFailure) =>
                    {
                        if (key.IsNullOrWhiteSpace() || key == ".")
                        {
                            try
                            {
                                var rootObject = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                    contentString, type, bindConvert);
                                return onFound(rootObject);
                            }
                            catch (Exception ex)
                            {
                                return onFailure(ex.Message);
                            }
                        }

                        if (!contentJObject.TryGetValue(key, out Newtonsoft.Json.Linq.JToken valueToken))
                            return await onFailure($"Key[{key}] was not found in JSON").AsTask();

                        try
                        {
                            var tokenParser = new JsonTokenParser(valueToken);
                            return ContentToTypeAsync(type, tokenParser,
                                obj => onFound(obj),
                                (why) =>
                                {
                                    if (valueToken.Type != JTokenType.Object)
                                        return onFailure(why);
                                    try
                                    {
                                        var value = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                            valueToken.ToString(), type, bindConvert);
                                        // var value = valueToken.ToObject(type);
                                        return onFound(value);
                                    }
                                    catch (Newtonsoft.Json.JsonSerializationException)
                                    {
                                        throw;
                                    }
                                });
                        }
                        catch (Exception ex)
                        {
                            return onFailure(ex.Message);
                        }
                    };
                var keys = contentJObject
                        .Properties()
                        .Select(jProperty => jProperty.Name)
                        .ToArray();
                return await onParsedContentValues(parser, keys);
            }

            if (content.IsMimeMultipartContent())
            {
                var streamProvider = new MultipartMemoryStreamProvider();
                await content.ReadAsMultipartAsync(streamProvider);
                var contentsLookup = await streamProvider.Contents
                    .SelectAsyncOptional<HttpContent, KeyValuePair<string, IParseToken>>(
                        async (file, select, skip) =>
                        {
                            if (file.IsDefaultOrNull())
                                return skip();

                            var key = file.Headers.ContentDisposition.Name.Trim(new char[] { '"' });
                            var fileNameMaybe = file.Headers.ContentDisposition.FileName;
                            if (null != fileNameMaybe)
                                fileNameMaybe = fileNameMaybe.Trim(new char[] { '"' });
                            var contents = await file.ReadAsByteArrayAsync();

                            var kvp = key.PairWithValue<string, IParseToken>(
                                    new MultipartContentTokenParser(contents, fileNameMaybe));
                            return select(kvp);
                        })
                    .ToDictionaryAsync();
                ParseContentDelegate<TParseResult> parser =
                        async (key, type, onFound, onFailure) =>
                        {
                            if (contentsLookup.ContainsKey(key))
                                return ContentToTypeAsync(type, contentsLookup[key],
                                    onFound,
                                    onFailure);
                            return onFailure("Key not found");
                        };
                return await onParsedContentValues(parser, contentsLookup.SelectKeys().ToArray());
            }

            if (content.IsFormData())
            {
                var optionalFormData = (await this.ParseOptionalFormDataAsync(content)).ToDictionary();
                ParseContentDelegate<TParseResult> parser =
                    async (key, type, onFound, onFailure) =>
                    {
                        if (!optionalFormData.ContainsKey(key))
                            return onFailure("Key not found");
                        return this.Bind(type, optionalFormData[key],
                            (value) => onFound(value),
                            (why) => onFailure(why));
                    };
                return await onParsedContentValues(parser, optionalFormData.SelectKeys().ToArray());
            }

            var mediaType = content.Headers.IsDefaultOrNull() ?
                string.Empty
                :
                content.Headers.ContentType.IsDefaultOrNull() ?
                    string.Empty
                    :
                    content.Headers.ContentType.MediaType;
            return await InvalidContent($"Could not parse content of type {mediaType}");
        }

        private async Task<KeyValuePair<string, IParseToken>[]> ParseOptionalFormDataAsync(HttpContent content)
        {
            var formData = await content.ReadAsFormDataAsync();

            var parameters = formData.AllKeys
                .Select(key => key.PairWithValue<string, IParseToken>(
                    new FormDataTokenParser(formData[key])))
                .ToArray();

            return (parameters);
        }

        private TResult ContentToTypeAsync<TResult>(Type type, 
            IParseToken tokenReader,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = tokenReader.ReadStream();
                return onParsed((object)streamValue);
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var byteArrayValue = tokenReader.ReadBytes();
                return onParsed((object)byteArrayValue);
            }
            return this.Bind(type, tokenReader,
                (value) =>
                {
                    return onParsed(value);
                },
                why => onFailure(why));
        }

        internal bool CanExtrude(Type type)
        {
            if (this.bindings.ContainsKey(type))
                return true;

            if (type.IsGenericType)
            {
                var possibleGenericInstigator = this.bindingsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                return possibleGenericInstigator.Any();
            }

            return false;
        }

        #endregion
    }
}
