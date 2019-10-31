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
using System.Net.Http.Headers;
using System.Xml;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Api
{
    [ApiResourcesAttribute(NameSpacePrefixes = "EastFive.Api,EastFive.Web")]
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

        //public virtual async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        //{
        //    using (var client = new HttpClient())
        //    {
        //        var response = await client.SendAsync(request);
        //        return response;
        //    }
        //}

        public TResult GetResourceType<TResult>(string resourceType,
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
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (!assembly.GlobalAssemblyCache))
                .Where(assembly => limitedAssemblyQuery
                    .First(
                        (q, n) =>
                        {
                            if (q.ShouldCheckAssembly(assembly))
                                return true;
                            return n();
                        },
                        () => false))
                .ToArray();

            lock (lookupLock)
            {
                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    if (args.LoadedAssembly.GlobalAssemblyCache)
                        return;
                    var check = limitedAssemblyQuery
                        .First(
                            (q, n) =>
                            {
                                if (q.ShouldCheckAssembly(args.LoadedAssembly))
                                    return true;
                                return n();
                            },
                            () => false);
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

        #endregion

        #region Instigators - Concrete

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
                    (httpApp, request, paramInfo, success) => success(httpApp.Logger)
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
            var instigationAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatable>();
            if (instigationAttrs.Any())
            {
                var instigationAttr = instigationAttrs.First();
                return instigationAttr.Instigate(this, request, methodParameter,
                    (v) => onInstigated(v));
            }

            var instigationGenericAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatableGeneric>();
            if (instigationGenericAttrs.Any())
            {
                var instigationAttr = instigationGenericAttrs.First();
                return instigationAttr.InstigatorDelegateGeneric(methodParameter.ParameterType, this, request, methodParameter,
                    (v) => onInstigated(v));
            }

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
                    typeof(DateTimeOffset),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var dateStringValue = token.ReadString();
                        if (DateTimeOffset.TryParse(dateStringValue, out DateTimeOffset dateValue))
                            return onParsed(dateValue);
                        return onNotConvertable($"Failed to convert {dateStringValue} to `{typeof(DateTimeOffset).FullName}`.");
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
                    typeof(double),
                    (httpApp, token, onParsed, onNotConvertable) =>
                    {
                        var intStringValue = token.ReadString();
                        if (double.TryParse(intStringValue, out double intValue))
                            return onParsed(intValue);
                        return onNotConvertable($"Failed to convert {intStringValue} to `{typeof(double).FullName}`.");
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
                        } catch(Exception)
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
                        } catch(Exception)
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
                        } catch (Exception)
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
                if (type.IsNullable())
                {
                    if (!CanBind(type.GenericTypeArguments.First()))
                    {
                        return false;
                    }
                }

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

        public TResult Bind<T, TResult>(Serialization.IParseToken content,
            Func<T, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            return Bind(typeof(T), content,
                (obj) =>
                {
                    if (obj.IsDefaultOrNull())
                        return onParsed((T)obj);
                    
                    if (typeof(T).IsAssignableFrom(obj.GetType()))
                    {
                        var result = (T)obj;
                        return onParsed(result);
                    }
                    return onDidNotBind($"Could not cast {obj.GetType().FullName} to `{typeof(T).FullName}`.");
                }, 
                onDidNotBind);
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
            
            if(type.IsEnum)
            {
                var stringValue = content.ReadString();
                object value;
                try
                {
                    value = Enum.Parse(type, stringValue);
                } catch (Exception)
                {
                    var validValues = Enum.GetNames(type).Join(", ");
                    return onDidNotBind($"Value `{stringValue}` is not a valid value for `{type.FullName}.` Valid values are [{validValues}].");
                }
                return onParsed(value);
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
            if (this.bindingsGeneric.ContainsKey(type))
                this.instantiationsGeneric[type] = instantiation;
            else
                this.instantiationsGeneric.Add(type, instantiation);
        }

        #endregion

        #region Conversions

        public virtual async Task<TResult> ParseContentValuesAsync<TParseResult, TResult>(HttpContent content,
            Func<ParseContentDelegateAsync<TParseResult>, string [], Task<TResult>> onParsedContentValues)
        {
            Task<TResult> InvalidContent(string errorMessage)
            {
                ParseContentDelegateAsync<TParseResult> parser =
                    (app, request, paramInfo, onFound, onFailure) =>
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
                    ParseContentDelegateAsync<TParseResult> exceptionParser =
                        (paramInfo, app, request, onFound, onFailure) =>
                        {
                            var key = paramInfo
                                        .GetAttributeInterface<IBindApiValue>()
                                        .GetKey(paramInfo)
                                        .ToLower();
                            var type = paramInfo.ParameterType;
                            return onFailure($"[{key}] was not provided (JSON body content was empty).").AsTask();
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
                    ParseContentDelegateAsync<TParseResult> exceptionParser =
                        (app, request, paramInfo, onFound, onFailure) =>
                        {
                            return onFailure(ex.Message).AsTask();
                        };
                    return await onParsedContentValues(exceptionParser, exceptionKeys);
                }
                ParseContentDelegateAsync<TParseResult> parser =
                    (paramInfo, app, request, onFound, onFailure) =>
                    {
                        return paramInfo
                            .GetAttributeInterface<IBindJsonApiValue>()
                            .ParseContentDelegateAsync(contentJObject,
                                    contentString, bindConvert,
                                    paramInfo, app, request,
                                onFound,
                                onFailure);
                    };
                var keys = contentJObject
                        .Properties()
                        .Select(jProperty => jProperty.Name)
                        .ToArray();
                return await onParsedContentValues(parser, keys);
            }

            if (content.IsXml())
            {
                var contentString = await content.ReadAsStringAsync();

                var exceptionKeys = new string[] { };
                if (!contentString.HasBlackSpace())
                {
                    ParseContentDelegateAsync<TParseResult> exceptionParser =
                        (paramInfo, app, request, onFound, onFailure) =>
                        {
                            var key = paramInfo
                                        .GetAttributeInterface<IBindApiValue>()
                                        .GetKey(paramInfo)
                                        .ToLower();
                            return onFailure($"[{key}] was not provided (JSON body content was empty).").AsTask();
                        };
                    return await onParsedContentValues(exceptionParser, exceptionKeys);
                }

                var xmldoc = new XmlDocument();
                try
                {
                    xmldoc.LoadXml(contentString);
                }
                catch (Exception ex)
                {
                    ParseContentDelegateAsync<TParseResult> exceptionParser =
                        async (app, request, paramInfo, onFound, onFailure) =>
                        {
                            return onFailure(ex.Message);
                        };
                    return await onParsedContentValues(exceptionParser, exceptionKeys);
                }

                ParseContentDelegateAsync<TParseResult> parser =
                    (paramInfo, app, request, onFound, onFailure) =>
                    {
                        return paramInfo
                            .GetAttributeInterface<IBindXmlApiValue>()
                            .ParseContentDelegateAsync(xmldoc, contentString,
                                paramInfo, app, request,
                                onFound,
                                onFailure);
                    };
                return await onParsedContentValues(parser, new string[] { });
            }

            if (content.IsMimeMultipartContent())
            {
                var streamProvider = new MultipartMemoryStreamProvider();
                try
                {
                    await content.ReadAsMultipartAsync(streamProvider);
                } catch(System.IO.IOException readError)
                {
                    ParseContentDelegateAsync<TParseResult> errorParser =
                        (app, request, paramInfo, onFound, onFailure) =>
                        {
                            if(readError.InnerException.IsDefaultOrNull())
                                return onFailure(readError.Message).AsTask();

                            return onFailure($"{readError.Message}:{readError.InnerException.Message}").AsTask();
                        };
                    return await onParsedContentValues(errorParser, new string[] { });
                }
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
                ParseContentDelegateAsync<TParseResult> parser =
                        async (paramInfo, app, request, onFound, onFailure) =>
                        {
                            var key = paramInfo
                                        .GetAttributeInterface<IBindApiValue>()
                                        .GetKey(paramInfo);
                            var type = paramInfo.ParameterType;
                            if (contentsLookup.ContainsKey(key))
                                return ContentToType(type, contentsLookup[key],
                                    onFound,
                                    onFailure);
                            return await onFailure("Key not found").AsTask();
                        };
                return await onParsedContentValues(parser, contentsLookup.SelectKeys().ToArray());
            }

            if (content.IsFormData())
            {
                var optionalFormData = (await ParseOptionalFormDataAsync(content)).ToDictionary();
                ParseContentDelegateAsync<TParseResult> parser =
                    async (paramInfo, app, request, onFound, onFailure) =>
                    {
                        var key = paramInfo
                                    .GetAttributeInterface<IBindApiValue>()
                                    .GetKey(paramInfo);
                        var type = paramInfo.ParameterType;
                        if (!optionalFormData.ContainsKey(key))
                            return await onFailure("Key not found").AsTask();
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

        private static async Task<KeyValuePair<string, IParseToken>[]> ParseOptionalFormDataAsync(HttpContent content)
        {
            var formData = await content.ReadAsFormDataAsync();

            var parameters = formData.AllKeys
                .Select(key => key.PairWithValue<string, IParseToken>(
                    new FormDataTokenParser(formData[key])))
                .ToArray();

            return (parameters);
        }

        private TResult ContentToType<TResult>(Type type, 
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
            if (type.IsAssignableFrom(typeof(ByteArrayContent)))
            {
                var content = tokenReader.ReadObject<ByteArrayContent>();
                return onParsed((object)content);
            }
            return this.Bind(type, tokenReader,
                (value) =>
                {
                    return onParsed(value);
                },
                why => onFailure(why));
        }

        public IEnumerable<MethodInfo> GetExtensionMethods(Type controllerType)
        {
            if (routeResourceExtensionLookup.ContainsKey(controllerType))
                return routeResourceExtensionLookup[controllerType];
            return new MethodInfo[] { };
        }

        #endregion
    }
}
