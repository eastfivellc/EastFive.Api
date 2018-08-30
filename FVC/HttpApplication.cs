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
        }

        protected virtual void Application_Start()
        {
            LocateControllers();
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
                    .Where(type => type.IsClass && type.ContainsCustomAttribute<FunctionViewControllerAttribute>())
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

        public delegate Task<HttpResponseMessage> InstigatorDelegate(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);

        public Dictionary<Type, InstigatorDelegate> instigators =
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
                        Controllers.RedirectResponse dele = (redirectLocation) => request.CreateRedirectResponse(redirectLocation);
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
                                } catch(Exception ex)
                                {
                                    var response = request.CreateResponse(HttpStatusCode.InternalServerError).AddReason($"Could not load template {viewPath}");
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

        #endregion

        #region Conversions

        public async Task<KeyValuePair<string, Func<Type, object>>[]> ParseOptionalFormDataAsync(HttpContent content)
        {
            var formData = await content.ReadAsFormDataAsync();

            var parameters = formData.AllKeys
                .Select(key => key.PairWithValue<string, Func<Type, object>>(
                    (type) => StringContentToType(type, formData[key],
                        v => v,
                        why => { throw new Exception(why); })))
                .ToArray();

            return (parameters);
        }

        public virtual async Task<KeyValuePair<string, Func<Type, object>>[]> ParseContentValuesAsync(HttpContent content)
        {
            if (content.IsDefaultOrNull())
                return (new KeyValuePair<string, Func<Type, object>>[] { });

            if (
                (!content.Headers.IsDefaultOrNull()) &&
                (!content.Headers.ContentType.IsDefaultOrNull()) &&
                String.Compare("application/json", content.Headers.ContentType.MediaType, true) == 0)
            {
                var contentString = await content.ReadAsStringAsync();
                try
                {
                    var contentJObject = Newtonsoft.Json.Linq.JObject.Parse(contentString);
                    return contentJObject
                        .Properties()
                        .Select(
                            jProperty =>
                            {
                                var key = jProperty.Name;
                                return key.PairWithValue<string, Func<Type, object>>(
                                    (type) =>
                                    {
                                        try
                                        {
                                            return jProperty.First.ToObject(type);
                                            // return Newtonsoft.Json.JsonConvert.DeserializeObject(value, type).ToTask();
                                        }
                                        catch (Exception ex)
                                        {
                                            return ((object)ex);
                                        }
                                    });
                            })
                        .ToArray();
                }
                catch (Exception ex)
                {
                }
            }

            if (content.IsMimeMultipartContent())
            {
                var streamProvider = new MultipartMemoryStreamProvider();
                await content.ReadAsMultipartAsync(streamProvider);

                return await streamProvider.Contents
                        .Select(
                            async file =>
                            {
                                var key = file.Headers.ContentDisposition.Name.Trim(new char[] { '"' });
                                var contents = await file.ReadAsByteArrayAsync();
                                if (file.IsDefaultOrNull())
                                    return key.PairWithValue<string, Func<Type, object>>(
                                        type => type.IsValueType ? Activator.CreateInstance(type) : null);

                                return key.PairWithValue<string, Func<Type, object>>(
                                    type => ContentToTypeAsync(type, () => System.Text.Encoding.UTF8.GetString(contents), () => contents, () => new MemoryStream(contents)));
                            })
                        .WhenAllAsync();
            }

            if (content.IsFormData())
            {
                var optionalFormData = await this.ParseOptionalFormDataAsync(content);
                return (
                    optionalFormData
                        .Select(
                            formDataCallbackKvp => formDataCallbackKvp.Key.PairWithValue<string, Func<Type, object>>(
                                (type) => formDataCallbackKvp.Value(type)))
                        .ToArray());
            }

            return (new KeyValuePair<string, Func<Type, object>>[] { });
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
