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
using EastFive.Reflection;
using RazorEngine.Templating;
using System.Security;
using System.Security.Permissions;
using EastFive.Api.Serialization;

namespace EastFive.Api
{
    public class HttpApplication : System.Web.HttpApplication, IApplication
    {
        private Task initialization;
        private ManualResetEvent initializationLock;

        public HttpApplication()
            : base()
        {
            initializationLock = new ManualResetEvent(false);
            this.initialization = InitializeAsync();
        }

        public override void Dispose()
        {
            Dispose(true);
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (initializationLock != null)
                {
                    initializationLock.Dispose();
                    initializationLock = null;
                }
                if (initialization != null)
                {
                    initialization.Dispose();
                    initialization = null;
                }
            }
        }

        public virtual string Namespace
        {
            get
            {
                return "";
            }
        }

        public void Application_Start()
        {
            System.Web.Mvc.AreaRegistration.RegisterAllAreas();
            ApplicationStart();
            GlobalConfiguration.Configure(this.Configure);
            Registration();
            SetupRazorEngine(string.Empty);
        }

        public virtual void ApplicationStart()
        {
            LocateControllers();
            //SetupRazorEngine();
        }

        public virtual void SetupRazorEngine()
        {
            SetupRazorEngine(string.Empty);
        }

        public static void SetupRazorEngine(string rootDirectory)
        {
            var templateManager = new Razor.RazorTemplateManager(rootDirectory);
            var referenceResolver = new Razor.GenericReferenceResolver();
            var config = new RazorEngine.Configuration.TemplateServiceConfiguration
            {
                TemplateManager = templateManager,
                ReferenceResolver = referenceResolver,
                BaseTemplateType = typeof(Razor.HtmlSupportTemplateBase<>),
                DisableTempFileLocking = true
            };
            RazorEngine.Engine.Razor = RazorEngine.Templating.RazorEngineService.Create(config);
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

        public Type[] GetResources()
        {
            return resources;
        }

        public TResult GetControllerMethods<TResult>(string routeName,
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
            if(null == value)
            {
                var nullDefaultValue = propertyType.GetDefault();
                return onCasted(value);
            }

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
        private static Type[] resources;
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

                resources = resources
                    .NullToEmpty()
                    .Concat(functionViewControllerAttributesAndTypes.SelectValues())
                    .Distinct(type => type.GUID)
                    .ToArray();

                lookup = lookup.Merge(
                    functionViewControllerAttributesAndTypes
                        .Select(
                            attrType =>
                            {
                                var actionMethods = attrType.Value
                                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                    .Where(method => method.ContainsCustomAttribute<HttpActionAttribute>())
                                    .GroupBy(method => method.GetCustomAttribute<HttpActionAttribute>().Method)
                                    .Select(methodGrp => (new HttpMethod(methodGrp.Key)).PairWithValue(methodGrp.ToArray()));

                                IDictionary<HttpMethod, MethodInfo[]> methods = methodLookup
                                        .Select(
                                            methodKvp => methodKvp.Value.PairWithValue(
                                                attrType.Value
                                                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
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

        public class InstigatorGenericWrapper1<T1>
        {
            public HttpApplication httpApp;
            public HttpRequestMessage request;
            public ParameterInfo paramInfo;

            public InstigatorGenericWrapper1(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo)
            {
                this.httpApp = httpApp;
                this.request = request;
                this.paramInfo = paramInfo;
            }

            HttpResponseMessage ContentTypeResponse(object content, string contentTypeString = default(string))
            {
                return typeof(T1)
                    .GetAttributesInterface<IProvideSerialization>()
                    .Select(
                        serializeAttr =>
                        {
                            var quality = request.Headers.Accept
                                .Where(acceptOption => acceptOption.MediaType.ToLower() == serializeAttr.MediaType.ToLower())
                                .First(
                                    (acceptOption, next) => acceptOption.Quality.HasValue ? acceptOption.Quality.Value : -1.0,
                                    () => -2.0);
                            return serializeAttr.PairWithValue(quality);
                        })
                    .OrderByDescending(kvp => kvp.Value)
                    .First(
                        (serializerQualityKvp, next) =>
                        {
                            var serializationProvider = serializerQualityKvp.Key;
                            var quality = serializerQualityKvp.Value;
                            var responseNoContent = request.CreateResponse(System.Net.HttpStatusCode.OK, content);
                            var customResponse = serializationProvider.Serialize(responseNoContent, httpApp, request, paramInfo, content);
                            return customResponse;
                        },
                        () =>
                        {
                            var response = request.CreateResponse(System.Net.HttpStatusCode.OK, content);
                            if (!contentTypeString.IsNullOrWhiteSpace())
                                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentTypeString);
                            return response;
                        });
            }

            HttpResponseMessage CreatedBodyResponse(object content, string contentTypeString = default(string))
            {
                var contentType = typeof(T1);
                if (!contentType.ContainsAttributeInterface<IProvideSerialization>())
                {
                    var response = request.CreateResponse(System.Net.HttpStatusCode.Created, content);
                    if (!contentTypeString.IsNullOrWhiteSpace())
                        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentTypeString);
                    return response;
                }

                var responseNoContent = request.CreateResponse(System.Net.HttpStatusCode.Created, content);
                var serializationProvider = contentType.GetAttributesInterface<IProvideSerialization>().Single();
                var customResponse = serializationProvider.Serialize(responseNoContent, httpApp, request, paramInfo, content);
                return customResponse;
            }
        }

        public Dictionary<Type, InstigatorDelegateGeneric> instigatorsGeneric =
            new Dictionary<Type, InstigatorDelegateGeneric>()
            {
                {
                    typeof(Controllers.ContentTypeResponse<>),
                    (type, httpApp, request, paramInfo, success) =>
                    {
                        var wrapperConcreteType = typeof(InstigatorGenericWrapper1<>).MakeGenericType(type.GenericTypeArguments);
                        var wrapperInstance = Activator.CreateInstance(wrapperConcreteType, new object [] { httpApp, request, paramInfo });
                        var dele = Delegate.CreateDelegate(type, wrapperInstance, "ContentTypeResponse", false);
                        return success((object)dele);
                    }
                },
                {
                    typeof(Controllers.CreatedBodyResponse<>),
                    (type, httpApp, request, paramInfo, success) =>
                    {
                        var wrapperConcreteType = typeof(InstigatorGenericWrapper1<>).MakeGenericType(type.GenericTypeArguments);
                        var wrapperInstance = Activator.CreateInstance(wrapperConcreteType, new object [] { httpApp, request, paramInfo });
                        var dele = Delegate.CreateDelegate(type, wrapperInstance, "CreatedBodyResponse", false);
                        return success((object)dele);
                    }
                },
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


        public static HttpResponseMessage CreatedBodyResponse(HttpRequestMessage request)
        {
            return request
                .CreateResponse(System.Net.HttpStatusCode.BadRequest)
                .AddReason("The query parameter did not reference an existing document.");
        }

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

                            var responseNoContent = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                            var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
                            var customResponse = serializationProvider.Serialize(responseNoContent, httpApp, request, paramInfo, obj);
                            return customResponse;
                        })
                    .Async();

                bool IsMultipart()
                {
                    var acceptHeader = request.Headers.Accept;
                    if (acceptHeader.IsDefaultOrNull())
                        return false;
                    if (request.Headers.Accept.Count == 0)
                    {
                        var hasMultipart = acceptHeader.ToString().ToLower().Contains("multipart");
                        return hasMultipart;
                    }
                    return false;
                }

                if (!IsMultipart())
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
                    typeof(EastFive.Api.Controllers.SessionToken?),
                    (httpApp, request, paramInfo, success) =>
                    {
                        return EastFive.Web.Configuration.Settings.GetString(AppSettings.ActorIdClaimType,
                            (accountIdClaimType) =>
                            {
                                return request.GetClaims(
                                    (claimsEnumerable) =>
                                    {
                                        var claims = claimsEnumerable.ToArray();
                                        return claims.GetAccountIdMaybe(
                                                request, accountIdClaimType,
                                            (accountIdMaybe) =>
                                            {
                                                var sessionIdClaimType = BlackBarLabs.Security.ClaimIds.Session;
                                                return claims.GetSessionIdAsync(
                                                    request, sessionIdClaimType,
                                                    (sessionId) =>
                                                    {
                                                        var token = new Controllers.SessionToken
                                                        {
                                                            accountIdMaybe = accountIdMaybe,
                                                            sessionId = sessionId,
                                                            claims = claims,
                                                        };
                                                        return success(token);
                                                    });
                                            });
                                    },
                                    () => success(default(EastFive.Api.Controllers.SessionToken?)),
                                    (why) => success(default(EastFive.Api.Controllers.SessionToken?)));
                            },
                            (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());
                    }
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
                        return EastFive.Web.Configuration.Settings.GetString(AppSettings.ActorIdClaimType,
                            (accountIdClaimType) =>
                            {
                                return request.GetClaims(
                                    (claimsEnumerable) =>
                                    {
                                        var claims = claimsEnumerable.ToArray();
                                        return claims.GetAccountIdMaybe(
                                                request, accountIdClaimType,
                                            (accountIdMaybe) =>
                                            {
                                                var sessionIdClaimType = BlackBarLabs.Security.ClaimIds.Session;
                                                return claims.GetSessionIdAsync(
                                                    request, sessionIdClaimType,
                                                    (sessionId) =>
                                                    {
                                                        var token = new Controllers.SessionToken
                                                        {
                                                            accountIdMaybe = accountIdMaybe,
                                                            sessionId = sessionId,
                                                            claims = claims,
                                                        };
                                                        return success(token);
                                                    });
                                            });
                                    },
                                    () => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason("Authorization header not set.").AsTask(),
                                    (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());
                            },
                            (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());
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
                                .AddReason($"`{configurationValue}` not specified in config:{message}");
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
                        Controllers.AlreadyExistsReferencedResponse dele = (existingId) => request.CreateResponse(System.Net.HttpStatusCode.Conflict).AddReason($"There is already a resource with ID = [{existingId}]");
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

                                var responseNoContent = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                                var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
                                var customResponse = serializationProvider.Serialize(responseNoContent, httpApp, request, paramInfo, obj);
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
                    typeof(Controllers.ExecuteBackgroundResponseAsync),
                    (httpApp, request, paramInfo, success) =>
                    {
                        Controllers.ExecuteBackgroundResponseAsync dele =
                            async (executionContext) =>
                            {
                                bool shouldRunInBackground()
                                {
                                    if (executionContext.ForceBackground)
                                        return true;

                                    if(request.Headers.Accept.Contains(mediaType => mediaType.MediaType.ToLower().Contains("background")))
                                        return true;

                                    return false;
                                }

                                if(shouldRunInBackground())
                                {
                                    var urlHelper = request.GetUrlHelper();
                                    var processId = Controllers.BackgroundProgressController.CreateProcess(
                                        async updateCallback =>
                                        {
                                            var completion = await executionContext.InvokeAsync(
                                                v =>
                                                {
                                                    updateCallback(v);
                                                });
                                            return completion;
                                        }, 1.0);
                                    var response = request.CreateResponse(HttpStatusCode.Accepted);
                                    response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
                                    response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<Controllers.BackgroundProgressController>(processId).AbsoluteUri);
                                    return response;
                                }
                                return await executionContext.InvokeAsync(v => { });
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

        public void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false)
        {
            instigators[type] = instigator;
        }

        public void AddGenericInstigator(Type type, InstigatorDelegateGeneric instigator)
        {
            instigatorsGeneric.Add(type, instigator);
        }

        public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator, bool clear = false)
        {
            instigatorsGeneric[type] = instigator;
        }

        public Task<HttpResponseMessage> Instigate(HttpRequestMessage request, ParameterInfo methodParameter,
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

        public delegate object BindingDelegate(HttpApplication httpApp, Serialization.IParseToken content,
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
                        if(webId.IsDefaultOrNull())
                            return onNotConvertable("Null value for GUID.");
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
                    typeof(int),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var intStringValue = token.ReadString();
                        if (int.TryParse(intStringValue, out int intValue))
                            return onParsed(intValue);
                        return onNotConvertable($"Failed to convert {intStringValue} to `{typeof(int).FullName}`.");
                    }
                },
                {
                    typeof(decimal?),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var intStringValue = token.ReadObject<decimal?>();
                        //if (int.TryParse(intStringValue, out int intValue))
                        //    return onParsed(intValue);
                        return onParsed(intStringValue);
                        //return onNotConvertable($"Failed to convert {intStringValue} to `{typeof(int).FullName}`.");
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
                    typeof(Uri),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var uriStringValue = token.ReadString();
                        if (Uri.TryCreate(uriStringValue, UriKind.RelativeOrAbsolute, out Uri uriValue))
                            return onParsed(uriValue);
                        return onNotConvertable($"Failed to convert {uriStringValue} to `{typeof(Uri).FullName}`.");
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
                        var objValue = content.ReadObject();
                        return onParsed(objValue);
                    }
                }
            };

        public delegate object BindingGenericDelegate(Type type, HttpApplication httpApp, Serialization.IParseToken content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        protected Dictionary<Type, BindingGenericDelegate> bindingsGeneric =
            new Dictionary<Type, BindingGenericDelegate>()
            {
                {
                    typeof(IRef<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        return httpApp.Bind(typeof(Guid), content,
                            (id) =>
                            {
                                var resourceType = type.GenericTypeArguments.First();
                                var instantiatableType = typeof(EastFive.Ref<>).MakeGenericType(resourceType);
                                var instance = Activator.CreateInstance(instantiatableType, new object[] { id });
                                return onBound(instance);
                            },
                            (why) => onFailedToBind(why));
                    }
                },
                {
                    typeof(IRefObj<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        var resourceType = type.GenericTypeArguments.First();
                        var instantiatableType = typeof(EastFive.RefObj<>).MakeGenericType(resourceType);

                        return httpApp.Bind(typeof(Guid), content,
                            (id) =>
                            {
                                var instance = Activator.CreateInstance(instantiatableType, new object[] { id });
                                return onBound(instance);
                            },
                            (why) => onFailedToBind(why));
                    }
                },
                {
                    typeof(IRefOptional<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        var referredType = type.GenericTypeArguments.First();
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
                    typeof(IRefs<>),
                    (type, httpApp, content, onBound, onFailedToBind) =>
                    {
                        return httpApp.Bind(typeof(Guid[]), content,
                            (ids) =>
                            {
                                var resourceType = type.GenericTypeArguments.First();
                                var instantiatableType = typeof(Refs<>).MakeGenericType(resourceType);
                                var instance = Activator.CreateInstance(instantiatableType, new object[] { ids });
                                return onBound(instance);
                            },
                            (why) => onFailedToBind(why));
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

        public TResult Bind<TResult>(Type type, Serialization.IParseToken content,
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

            var possibleInstantiators = this.instantiations
                .Where(
                    intatiatorKvp =>
                    {
                        var instantiatorType = intatiatorKvp.Key;
                        if (instantiatorType.IsSubClassOfGeneric(type))
                            return true;
                        return false;
                    });
            if (possibleInstantiators.Any())
            {
                var instance = await possibleInstantiators.First().Value(this);
                var castedVal = onParsed(instance);
                return castedVal;
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
                        if(instigatorKvp.Key.IsInterface)
                            if (instigatorKvp.Key.IsSubClassOfGeneric(type))
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
                var exceptionKeys = new string[] { };
                if (!contentString.HasBlackSpace())
                {
                    ParseContentDelegate<TParseResult> exceptionParser =
                        async (key, type, onFound, onFailure) =>
                        {
                            return onFailure($"[{key}] was not provided (JSON body content was empty).");
                        };
                    return await onParsedContentValues(exceptionParser, exceptionKeys);
                }
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
                                    if (valueToken.Type == JTokenType.Object || valueToken.Type == JTokenType.Array)
                                    {
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
                                    }
                                    return onFailure(why);
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
                                    new MultipartContentTokenParser(file, contents, fileNameMaybe));
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
            if (type.IsAssignableFrom(typeof(HttpContent)))
            {
                var content = tokenReader.ReadObject<HttpContent>();
                return onParsed((object)content);
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
