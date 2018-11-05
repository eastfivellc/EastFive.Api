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

namespace EastFive.Api
{
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

        protected void Application_Start()
        {
            System.Web.Mvc.AreaRegistration.RegisterAllAreas();
            ApplicationStart();
            GlobalConfiguration.Configure(this.Configure);
            Registration();
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

        public Type GetResourceType(string resourceType)
        {
            return contentTypeLookup.First(
                (kvp, next) =>
                {
                    if (!kvp.Value.Contains(resourceType))
                        return next();
                    return kvp.Key;
                },
                () => typeof(object));
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

        public void AddController(Type type, ControllerHandlerDelegate instigator)
        {
            var typeName = type.GetCustomAttribute<EastFive.Api.HttpResourceAttribute,string>(
                attr => attr.ResourceName.IsNullOrWhiteSpace()?
                    type.FullName
                    :
                    attr.ResourceName,
                () => type.FullName);
            urlHandlersByType.Add(type, instigator);
            urlHandlersByTypeName.Add(typeName, instigator);
        }

        public KeyValuePair<string, KeyValuePair<HttpMethod, MethodInfo[]>[]>[] GetLookups()
        {
            return lookup.Select(kvp => kvp.Key.PairWithValue(kvp.Value.ToArray())).ToArray();
        }

        internal TResult GetControllerMethods<TResult>(string routeName,
            Func<IDictionary<HttpMethod, MethodInfo[]>, TResult> onMethodsIdentified, 
            Func<TResult> onKeyNotFound)
        {
            if (!lookup.ContainsKey(routeName))
                return onKeyNotFound();
            var possibleHttpMethods = lookup[routeName];
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
                                IDictionary<HttpMethod, MethodInfo[]> methods = methodLookup
                                        .Select(
                                            methodKvp => methodKvp.Value.PairWithValue(
                                                attrType.Value
                                                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                    .Where(method => method.ContainsCustomAttribute(methodKvp.Key))
                                            .ToArray()))
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
                    .Select(obj => request.CreateResponse(System.Net.HttpStatusCode.OK, obj))
                    .Async();
                return await request.CreateMultipartResponseAsync(responses);
            }
        }

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
                        Controllers.ContentResponse dele = (obj, contentType) =>
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
                    typeof(EastFive.Api.Controllers.ViewFileResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        EastFive.Api.Controllers.ViewFileResponse dele =
                            (viewPath, content) =>
                            {
                                try
                                {
                                    using(var filestream = System.IO.File.OpenText($"{HttpRuntime.AppDomainAppPath}Views\\{viewPath}"))
                                    {
                                        var viewContent = filestream.ReadToEnd();
                                        var response = request.CreateResponse(HttpStatusCode.OK);
                                        var parsedView =  RazorEngine.Razor.Parse(viewContent, content);
                                        response.Content = new StringContent(parsedView);
                                        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
                                        return response;
                                    }
                                }
                                catch(RazorEngine.Templating.TemplateCompilationException ex)
                                {
                                    var body = ex.CompilerErrors.Select(error => error.ErrorText).Join(";\n\n");

                                    var response = request.CreateResponse(HttpStatusCode.InternalServerError, body)
                                        .AddReason($"Error loading template:[{ex.Message}] `{ex.SourceCode}`");
                                    return response;
                                } catch(Exception ex)
                                {
                                    var response = request.CreateResponse(HttpStatusCode.InternalServerError).AddReason($"Could not load template {HttpRuntime.AppDomainAppPath}Views\\{viewPath} due to:[{ex.GetType().FullName}] `{ex.Message}`");
                                    return response;
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

        public delegate object BindingDelegate(HttpApplication httpApp, string content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        protected Dictionary<Type, BindingDelegate> bindings =
            new Dictionary<Type, BindingDelegate>()
            {
                {
                    typeof(string),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var stringValue = content;
                        return onParsed((object)stringValue);
                    }
                },
                {
                    typeof(Guid),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var guidStringValue = content;
                        if (Guid.TryParse(guidStringValue, out Guid guidValue))
                            return onParsed(guidValue);
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Guid).FullName}`.");
                    }
                },
                {
                    typeof(DateTime),
                    (httpApp, dateStringValue, onParsed, onNotConvertable) =>
                    {
                        if (DateTime.TryParse(dateStringValue, out DateTime dateValue))
                            return onParsed(dateValue);
                        return onNotConvertable($"Failed to convert {dateStringValue} to `{typeof(DateTime).FullName}`.");
                    }
                },
                {
                    typeof(bool),
                    (httpApp, boolStringValue, onParsed, onNotConvertable) =>
                    {
                        if (bool.TryParse(boolStringValue, out bool boolValue))
                            return onParsed(boolValue);

                        if ("t" == boolStringValue)
                            return onParsed(true);

                        if ("f" == boolStringValue)
                            return onParsed(false);

                        return onNotConvertable($"Failed to convert {boolStringValue} to `{typeof(bool).FullName}`.");
                    }
                },
                {
                    typeof(Type),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        return onParsed(httpApp.GetResourceType(content));
                        // TODO: CHeck for type object or some method of failure and: return onNotConvertable($"`{content}` is not a web ID of none. Please use format `none` to specify no web ID provided.");
                    }
                },
                {
                    typeof(Stream),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var byteArrayBase64 = content;
                        try
                        {
                            var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                            return onParsed(new MemoryStream(byteArrayValue));
                        } catch(Exception ex)
                        {
                            return onNotConvertable($"Failed to convert {content} to `{typeof(Stream).FullName}` as base64 string:{ex.Message}.");
                        }
                    }
                },
                {
                    typeof(byte[]),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        var byteArrayBase64 = content;
                        try
                        {
                            var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                            return onParsed(byteArrayValue);
                        } catch(Exception ex)
                        {
                            return onNotConvertable($"Failed to convert {content} to `{typeof(byte[]).FullName}` as base64 string:{ex.Message}.");
                        }
                    }
                },
                {
                    typeof(Controllers.WebIdAny),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if (String.Compare(content.ToLower(), "any") == 0)
                            return onParsed(new Controllers.WebIdAny());
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Controllers.WebIdAny).FullName}`.");
                    }
                },
                {
                    typeof(Controllers.WebIdNone),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if (String.Compare(content.ToLower(), "none") == 0)
                            return onParsed(new Controllers.WebIdNone());
                        return onNotConvertable($"`{content}` is not a web ID of none. Please use format `none` to specify no web ID provided.");
                    }
                },
                {
                    typeof(Controllers.WebIdNot),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        return content.ToUpper().MatchRegexInvoke("NOT\\((?<notString>[a-zA-Z]+)\\)",
                            (string notString) => notString,
                            (string[] notStrings) =>
                            {
                                if (!notStrings.Any())
                                    return onNotConvertable($"`{content}` is not parsable as an exlusion list. Please use format `NOT(ABC123-....-EDF1)`");
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
                        if (String.Compare(content.ToLower(), "false") == 0)
                            return onParsed(new Controllers.DateTimeEmpty());
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Controllers.DateTimeEmpty).FullName}`.");
                    }
                },
                {
                    typeof(Controllers.DateTimeQuery),
                    (httpApp, content, onParsed, onNotConvertable) =>
                    {
                        if(DateTime.TryParse(content, out DateTime startEnd))
                            return onParsed(new Controllers.DateTimeQuery(startEnd, startEnd));
                        return onNotConvertable($"Failed to convert {content} to `{typeof(Controllers.DateTimeQuery).FullName}`.");
                    }
                },
            };

        public delegate object BindingGenericDelegate(Type type, HttpApplication httpApp, string content,
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
                        var refInstance = Activator.CreateInstance(refType, 
                            new object [] { referredType.GetDefault().AsTask() });
                        return refInstance;
                    }
                }
            };

        internal TResult Bind<TResult>(Type type, string content,
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
                    .Where(instigatorKvp => instigatorKvp.Key.GUID == type.GUID)
                    .ToArray();
                if (possibleGenericInstigator.Any())
                    return (TResult)possibleGenericInstigator.First().Value(type,
                            this, content,
                        (v) => onParsed(v),
                        (why) => onDidNotBind(why));
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

        #region Conversions



        public class MemoryStreamForFile : MemoryStream
        {
            public MemoryStreamForFile(byte[] buffer) : base(buffer) { }
            public string FileName { get; set; }
        }

        public delegate Task<TResult> ParseContentDelegate<TResult>(string key, Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

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
                try
                {
                    var contentJObject = Newtonsoft.Json.Linq.JObject.Parse(contentString);
                    ParseContentDelegate<TParseResult> parser =
                        async (key, type, onFound, onFailure) =>
                        {
                            if (key.IsNullOrWhiteSpace() || key == ".")
                            {
                                try
                                {
                                    var rootObject = Newtonsoft.Json.JsonConvert.DeserializeObject(contentString, type);
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
                                var value = valueToken.ToObject(type);
                                return onFound(value);
                            } catch(Exception ex)
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
                catch (Exception ex)
                {
                    ParseContentDelegate<TParseResult> parser =
                        async (key, type, onFound, onFailure) =>
                        {
                            return onFailure(ex.Message);
                        };
                    var keys = new string[] { };
                    return await onParsedContentValues(parser, keys);
                }
            }

            if (content.IsMimeMultipartContent())
            {
                var streamProvider = new MultipartMemoryStreamProvider();
                await content.ReadAsMultipartAsync(streamProvider);
                var contentsLookup = await streamProvider.Contents
                        .Select(
                            async file =>
                            {
                                var key = file.Headers.ContentDisposition.Name.Trim(new char[] { '"' });
                                var fileNameMaybe = file.Headers.ContentDisposition.FileName;
                                if (null != fileNameMaybe)
                                    fileNameMaybe = fileNameMaybe.Trim(new char[] { '"' });
                                var contents = await file.ReadAsByteArrayAsync();
                                if (file.IsDefaultOrNull())
                                    return key.PairWithValue<string, Func<Type, object>>(
                                        type => type.IsValueType ? Activator.CreateInstance(type) : null);

                                return key.PairWithValue<string, Func<Type, object>>(
                                    type => ContentToTypeAsync(type, () => System.Text.Encoding.UTF8.GetString(contents), () => contents, () => new MemoryStreamForFile(contents) { FileName = fileNameMaybe }));
                            })
                        .WhenAllAsync()
                        .ToDictionaryAsync();
                ParseContentDelegate<TParseResult> parser =
                        async (key, type, onFound, onFailure) =>
                        {
                            if (contentsLookup.ContainsKey(key))
                                return onFound(contentsLookup[key](type));
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
                        if (optionalFormData.ContainsKey(key))
                            return onFound(optionalFormData[key](type));
                        return onFailure("Key not found");
                    };
                return await onParsedContentValues(parser, optionalFormData.SelectKeys().ToArray());
            }

            return await InvalidContent($"Could not parse content of type {content.Headers.ContentType.MediaType}");
        }

        private async Task<KeyValuePair<string, Func<Type, object>>[]> ParseOptionalFormDataAsync(HttpContent content)
        {
            var formData = await content.ReadAsFormDataAsync();

            var parameters = formData.AllKeys
                .Select(key => key.PairWithValue<string, Func<Type, object>>(
                    (type) => Bind(type, formData[key],
                        v => v,
                        why => { throw new Exception(why); })))
                .ToArray();

            return (parameters);
        }

        private static object ContentToTypeAsync(Type type, Func<string> readString, Func<byte[]> readBytes, Func<Stream> readStream)
        {
            if (type.IsAssignableFrom(typeof(string)))
            {
                var stringValue = readString();
                return (object)stringValue;
            }
            if (type.IsAssignableFrom(typeof(Guid)))
            {
                var guidStringValue = readString();
                var guidValue = Guid.Parse(guidStringValue);
                return (object)guidValue;
            }
            if (type.IsAssignableFrom(typeof(bool)))
            {
                var boolStringValue = readString();
                if (bool.TryParse(boolStringValue, out bool boolValue))
                    return (object)boolValue;
                if (boolStringValue.ToLower() == "t")
                    return true;
                if (boolStringValue.ToLower() == "on") // used in check boxes
                    return true;
                return false;
            }
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = readStream();
                return (object)streamValue;
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var byteArrayValue = readBytes();
                return (object)byteArrayValue;
            }
            if (type.IsAssignableFrom(typeof(WebId)))
            {
                var guidStringValue = readString();
                var guidValue = Guid.Parse(guidStringValue);
                return (object)new WebId() { UUID = guidValue };
            }
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        public virtual TResult StringContentToType<TResult>(Type type, string content,
            Func<object, TResult> onParsed,
            Func<string, TResult> notConvertable)
        {
            if (type.IsAssignableFrom(typeof(string)))
            {
                var stringValue = content;
                return onParsed((object)stringValue);
            }
            if (type.IsAssignableFrom(typeof(Guid)))
            {
                var guidStringValue = content;
                if (Guid.TryParse(guidStringValue, out Guid guidValue))
                    return onParsed(guidValue);
            }
            if (type.IsAssignableFrom(typeof(DateTime)))
            {
                var dateStringValue = content;
                if (DateTime.TryParse(dateStringValue, out DateTime dateValue))
                    return onParsed(dateValue);
            }
            if (type.IsAssignableFrom(typeof(bool)))
            {
                var boolStringValue = content;
                if (bool.TryParse(boolStringValue, out bool boolValue))
                    return onParsed(boolValue);

                if ("t" == boolStringValue)
                    return onParsed(true);
                if ("f" == boolStringValue)
                    return onParsed(false);

                return onParsed(false);
            }
            if (type.IsAssignableFrom(typeof(Type)))
            {
                return onParsed(this.GetResourceType(content));
                return notConvertable($"`{content}` is not a web ID of none. Please use format `none` to specify no web ID provided.");
            }
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var byteArrayBase64 = content;
                var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                return onParsed(new MemoryStream(byteArrayValue));
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var byteArrayBase64 = content;
                var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                return (onParsed(byteArrayValue));
            }
            if (type.IsAssignableFrom(typeof(Controllers.WebIdAny)))
            {
                if (String.Compare(content.ToLower(), "any") == 0)
                    return onParsed(new Controllers.WebIdAny());
            }
            if (type.IsAssignableFrom(typeof(Controllers.WebIdNone)))
            {
                if (String.Compare(content.ToLower(), "none") == 0)
                    return onParsed(new Controllers.WebIdNone());
                return notConvertable($"`{content}` is not a web ID of none. Please use format `none` to specify no web ID provided.");
            }
            if (type.IsAssignableFrom(typeof(Controllers.WebIdNot)))
            {
                return content.ToUpper().MatchRegexInvoke("NOT\\((?<notString>[a-zA-Z]+)\\)",
                    (string notString) => notString,
                    (string[] notStrings) =>
                    {
                        if (!notStrings.Any())
                            return notConvertable($"`{content}` is not parsable as an exlusion list. Please use format `NOT(ABC123-....-EDF1)`");
                        var notString = notStrings.First();
                        if (!Guid.TryParse(notString, out Guid notUUID))
                            return notConvertable($"`{notString}` is not a UUID. Please use format `NOT(ABC123-....-EDF1)`");
                        return onParsed(
                            new Controllers.WebIdNot()
                            {
                                notUUID = notUUID,
                            });
                    });
            }
            if (type.IsAssignableFrom(typeof(Controllers.DateTimeEmpty)))
            {
                if (String.Compare(content.ToLower(), "false") == 0)
                    return onParsed(new Controllers.DateTimeEmpty());
            }

            return notConvertable($"Cannot convert `{content}` to type {type.FullName}");
        }

        #endregion
    }
}
