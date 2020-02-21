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
using System.Threading;
using System.Web.Http;
using EastFive.Web;
using EastFive.Linq.Async;
using Newtonsoft.Json.Linq;
using EastFive.Api.Serialization;
using System.Xml;
using System.Diagnostics;
using EastFive.Analytics;

namespace EastFive.Api
{
    [JsonContentParser]
    [XmlContentParser]
    [FormDataParser]
    [MimeMultipartContentParser]
    [ApiResources(NameSpacePrefixes = "EastFive.Api,EastFive.Web")]
    public class HttpApplication : System.Web.HttpApplication, IApplication
    {
        public virtual string Namespace
        {
            get
            {
                return "";
            }
        }

        #region Constructors / Destructors

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

        ~HttpApplication()
        {
            Dispose(false);
        }

        #endregion

        #region Initialization

        private Task initialization;
        private ManualResetEvent initializationLock;

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

        #endregion

        #region Logging

        public virtual EastFive.Analytics.ILogger Logger
        {
            get => new Analytics.ConsoleLogger();
        }

        public delegate Task StoreMonitoringDelegate(Guid monitorRecordId, Guid authenticationId, DateTime when, string method, string controllerName, string queryString);

        public virtual TResult DoesStoreMonitoring<TResult>(
            Func<StoreMonitoringDelegate, TResult> onMonitorUsingThisCallback,
            Func<TResult> onNoMonitoring)
        {
            return onNoMonitoring();
        }

        #endregion

        #region MVC Initialization

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

        protected virtual void Registration()
        {
        }

        protected virtual void Configure(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            DefaultApiRoute(config);

            config.MessageHandlers.Add(new Modules.ControllerHandler(config));
            config.MessageHandlers.Add(new Modules.MonitoringHandler(config));
        }

        public IHttpRoute DefaultApiRoute(HttpConfiguration config)
        {
            return config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }

        #endregion

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

        #region Url Handlers

        public static TResult GetResourceType<TResult>(string resourceType,
            Func<Type, TResult> onConverted,
            Func<TResult> onMatchingResourceNotFound)
        {
            return contentTypeLookup.First(
                (kvp, next) =>
                {
                    if (kvp.Value.ToLower().Trim() == resourceType.ToLower().Trim())
                        return onConverted(kvp.Key);
                    return next();
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

        public string GetResourceName(Type type)
        {
            if (type.IsDefaultOrNull())
                return string.Empty;
            var matches = resourceTypeRouteLookup
                .Where(nameTypeKvp => nameTypeKvp.Key == type);
            if (!matches.Any())
                return type.Name;
            return matches.First().Value;
        }

        public WebId GetResourceLink(string resourceType, Guid? resourceIdMaybe, UrlHelper url)
        {
            if (!resourceIdMaybe.HasValue)
                return default(WebId);
            return GetResourceLink(resourceType, resourceIdMaybe.Value, url);
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


        public Type[] GetResources()
        {
            return resources;
        }

        public TResult GetControllerType<TResult>(string routeName,
            Func<Type, TResult> onMethodsIdentified,
            Func<TResult> onKeyNotFound)
        {
            var routeNameLower = routeName.ToLower();
            if (!routeResourceTypeLookup.ContainsKey(routeNameLower))
                return onKeyNotFound();
            var controllerType = routeResourceTypeLookup[routeNameLower];
            return onMethodsIdentified(controllerType);
        }

        public Dictionary<Type, ControllerHandlerDelegate> urlHandlersByType =
            new Dictionary<Type, ControllerHandlerDelegate>()
            {
            };

        public Dictionary<string, ControllerHandlerDelegate> urlHandlersByTypeName =
            new Dictionary<string, ControllerHandlerDelegate>()
            {
            };

        #endregion

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
            if (null == value)
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
                if (value is DateTimeOffset)
                {
                    var dateValue = (DateTimeOffset)value;
                    var stringValue = dateValue.ToString();
                    return onCasted(stringValue);
                }
            }

            if (onNotMapped.IsDefaultOrNull())
                throw new Exception($"Cannot create {propertyType.FullName} from {value.GetType().FullName}");
            return onNotMapped();
        }


        #endregion

        #region Load Controllers

        private static IDictionary<string, Type> routeResourceTypeLookup;
        private static IDictionary<Type, MethodInfo[]> routeResourceExtensionLookup;
        private static Type[] resources;
        private static IDictionary<Type, string> contentTypeLookup;
        private static IDictionary<string, Type> resourceNameControllerLookup;
        private static IDictionary<Type, string> resourceTypeRouteLookup; // TODO: Delete after creating IDocumentParameter
        private static IDictionary<Type, Type> resourceTypeControllerLookup;
        private static object lookupLock = new object();

        private void LocateControllers()
        {
            var limitedAssemblyQuery = this.GetType()
                .GetAttributesInterface<IApiResources>(inherit: true, multiple: true);
            Func<Assembly, bool> shouldCheckAssembly =
                (assembly) =>
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
                };

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (!assembly.GlobalAssemblyCache))
                .Where(shouldCheckAssembly)
                .ToArray();

            lock (lookupLock)
            {
                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    if (args.LoadedAssembly.GlobalAssemblyCache)
                        return;
                    var check = shouldCheckAssembly(args.LoadedAssembly);
                    if (!check)
                        return;
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
                    .Where(type => type.ContainsAttributeInterface<IInvokeResource>())
                    .Select(
                        (type) =>
                        {
                            var attr = type.GetAttributesInterface<IInvokeResource>().First();
                            return type.PairWithKey(attr);
                        })
                    .ToArray();
                var duplicateRoutes = functionViewControllerAttributesAndTypes
                    .Duplicates((kvp1, kvp2) => kvp1.Key.Route == kvp2.Key.Route)
                    .Where(kvp => kvp.Key.Route.HasBlackSpace())
                    .Distinct(kvp => kvp.Key.Route);
                if (duplicateRoutes.Any())
                    throw new Exception($"Duplicate routes:{duplicateRoutes.SelectKeys(attr => attr.Route).Join(",")}");

                resources = resources
                    .NullToEmpty()
                    .Concat(functionViewControllerAttributesAndTypes.SelectValues())
                    .Distinct(type => type.GUID)
                    .ToArray();

                var extendedMethods = types
                    .Where(type => type.ContainsAttributeInterface<IInvokeExtensions>())
                    .SelectMany(
                        (type) =>
                        {
                            var attr = type.GetAttributesInterface<IInvokeExtensions>().First();
                            return attr.GetResourcesExtended(type);
                        })
                    .ToArray();
                routeResourceExtensionLookup = extendedMethods
                    .ToDictionaryCollapsed((t1, t2) => t1.FullName == t2.FullName)
                    .Concat(routeResourceExtensionLookup.NullToEmpty().Where(kvp => !extendedMethods.Contains(kvp2 => kvp2.Key == kvp.Key)))
                    .ToDictionary();

                routeResourceTypeLookup = routeResourceTypeLookup
                    .NullToEmpty()
                    .Concat(functionViewControllerAttributesAndTypes
                        .Select(
                            attrType =>
                            {
                                return attrType.Key.Route
                                    .IfThen(attrType.Key.Route.IsNullOrWhiteSpace(),
                                        (route) =>
                                        {
                                            var routeType = attrType.Key.Resource.IsDefaultOrNull() ?
                                                attrType.Value.Name
                                                :
                                                attrType.Key.Resource.Name;
                                            return routeType;
                                        })
                                    .ToLower()
                                    .PairWithValue(attrType.Value);
                            }))
                    .Distinct(kvp => kvp.Key)
                    .ToDictionary();

                resourceNameControllerLookup = resourceNameControllerLookup
                    .NullToEmpty()
                    .Concat(functionViewControllerAttributesAndTypes
                        .Select(
                            attrType =>
                            {
                                var contentType = GetContentType();
                                return contentType.PairWithValue(attrType.Value);
                                string GetContentType()
                                {
                                    if (!attrType.Key.ContentType.IsNullOrWhiteSpace())
                                        return attrType.Key.ContentType;
                                    var routeType = attrType.Key.Resource.IsDefaultOrNull() ?
                                        attrType.Value
                                        :
                                        attrType.Key.Resource;
                                    return $"x-application/{routeType.Name}";
                                }
                            }))
                    .Distinct(kvp => kvp.Key)
                    .ToDictionary();

                resourceTypeRouteLookup = resourceTypeRouteLookup
                    .NullToEmpty()
                    .Concat(functionViewControllerAttributesAndTypes
                        .Select(
                            attrType =>
                            {
                                return attrType.Key.Route
                                    .IfThen(attrType.Key.Route.IsNullOrWhiteSpace(),
                                        (route) =>
                                        {
                                            var routeType = attrType.Key.Resource.IsDefaultOrNull() ?
                                                attrType.Value.Name
                                                :
                                                attrType.Key.Resource.Name;
                                            return routeType;
                                        })
                                    .ToLower()
                                    .PairWithKey(attrType.Value);
                            }))
                    .Distinct(kvp => kvp.Key.FullName)
                    .ToDictionary();

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

        public Dictionary<Type, InstigatorDelegateGeneric> instigatorsGeneric =
            new Dictionary<Type, InstigatorDelegateGeneric>()
            {
            };

        public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator, bool clear = false)
        {
            instigatorsGeneric[type] = instigator;
        }

        #endregion

        #region Instigators - Concrete

        public const string DiagnosticsLogProperty = "X-Diagnostics-Log";

        protected Dictionary<Type, InstigatorDelegate> instigators =
            new Dictionary<Type, InstigatorDelegate>()
            {
                #region Security

                {
                    typeof(SessionToken?),
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
                                                        var token = new SessionToken
                                                        {
                                                            accountIdMaybe = accountIdMaybe,
                                                            sessionId = sessionId,
                                                            claims = claims,
                                                        };
                                                        return success(token);
                                                    });
                                            });
                                    },
                                    () => success(default(SessionToken?)),
                                    (why) => success(default(SessionToken?)));
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

                #endregion

                #region MVC System Objects

                {
                    typeof(System.Web.Http.Routing.UrlHelper),
                    (httpApp, request, paramInfo, success) => success(
                        new System.Web.Http.Routing.UrlHelper(request))
                },
                {
                    typeof(HttpRequestMessage),
                    (httpApp, request, paramInfo, success) => success(request)
                },

                #endregion

                #region Logging

                {
                    typeof(Analytics.ILogger),
                    async (httpApp, request, paramInfo, success) =>
                    {
                        if(!request.Headers.Contains("X-Diagnostics"))
                            return await success(httpApp.Logger);
                        var timer = new Stopwatch();
                        timer.Start();
                        var logger = new Analytics.CaptureLog(httpApp.Logger, timer);
                        var response = await success(logger);
                        logger.Trace("Response concluded.");
                        var diagnosticsLog = logger.Dump();
                        response.RequestMessage.Properties.Add(DiagnosticsLogProperty, diagnosticsLog);
                        // response.Content = new StringContent(responseString, Encoding.UTF8, "text/text");
                        return response;
                    }
                },

                #endregion

            };

        public void AddInstigator(Type type, InstigatorDelegate instigator)
        {
            instigators.Add(type, instigator);
        }

        public void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false)
        {
            instigators[type] = instigator;
        }

        public Task<HttpResponseMessage> Instigate(HttpRequestMessage request,
                 CancellationToken cancellationToken, ParameterInfo methodParameter,
            Func<object, Task<HttpResponseMessage>> onInstigated)
        {
            #region Check for app level override 

            var instigationAttrsApp = this.GetType()
                .GetAttributesInterface<IInstigate>()
                .Where(instigator => instigator.CanInstigate(methodParameter));
            if (instigationAttrsApp.Any())
            {
                var instigationAttr = instigationAttrsApp.First();
                return instigationAttr.Instigate(this,
                        request, cancellationToken, methodParameter,
                    onInstigated);
            }

            var instigationGenericAttrsApp = this.GetType()
                .GetAttributesInterface<IInstigateGeneric>()
                .Where(instigator => instigator.CanInstigate(methodParameter));
            if (instigationGenericAttrsApp.Any())
            {
                var instigationAttr = instigationGenericAttrsApp.First();
                return instigationAttr.InstigatorDelegateGeneric(methodParameter.ParameterType, this,
                        request, cancellationToken, methodParameter,
                    (v) => onInstigated(v));
            }

            #endregion

            var instigationAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatable>();
            if (instigationAttrs.Any())
            {
                var instigationAttr = instigationAttrs.First();
                return instigationAttr.Instigate(this,
                        request, cancellationToken, methodParameter,
                    onInstigated);
            }

            var instigationGenericAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatableGeneric>();
            if (instigationGenericAttrs.Any())
            {
                var instigationAttr = instigationGenericAttrs.First();
                return instigationAttr.InstigatorDelegateGeneric(methodParameter.ParameterType, this,
                        request, cancellationToken, methodParameter,
                    (v) => onInstigated(v));
            }

            if(methodParameter.ParameterType.IsAssignableFrom(typeof(CancellationToken)))
                return onInstigated(cancellationToken);

            if (methodParameter.ParameterType.IsAssignableFrom(typeof(HttpRequestMessage)))
                return onInstigated(request);

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

        #endregion

        #region Instantiations
        
        public delegate Task<object> InstantiationDelegate(HttpApplication httpApp);

        protected Dictionary<Type, InstantiationDelegate> instantiations =
            new Dictionary<Type, InstantiationDelegate>();

        public delegate Task<object> InstantiationGenericDelegate(Type type, HttpApplication httpApp);

        protected Dictionary<Type, InstantiationGenericDelegate> instantiationsGeneric =
            new Dictionary<Type, InstantiationGenericDelegate>()
            {
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
            if (this.instantiationsGeneric.ContainsKey(type))
                this.instantiationsGeneric[type] = instantiation;
            else
                this.instantiationsGeneric.Add(type, instantiation);
        }

        #endregion

        #region Conversions

        public IEnumerable<MethodInfo> GetExtensionMethods(Type controllerType)
        {
            if (routeResourceExtensionLookup.ContainsKey(controllerType))
                return routeResourceExtensionLookup[controllerType];
            return new MethodInfo[] { };
        }

        #endregion
    }
}
