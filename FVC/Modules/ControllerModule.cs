using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using EastFive.Serialization;
using System.Net.NetworkInformation;
using EastFive.Extensions;
using BlackBarLabs.Web;
using System.Reflection;
using System.Net.Http;
using EastFive.Linq;
using System.Net;
using BlackBarLabs.Api;
using BlackBarLabs;
using System.Threading;

namespace EastFive.Api.Modules
{
    public class ControllerModule : System.Net.Http.DelegatingHandler
    {
        private IDictionary<string, IDictionary<HttpMethod, MethodInfo[]>> lookup;
        private object lookupLock = new object();
        private System.Web.Http.HttpConfiguration config;

        public ControllerModule(System.Web.Http.HttpConfiguration config)
        {
            this.config = config;
            LocateControllers();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(!request.Properties.ContainsKey("MS_HttpContext"))
                return await base.SendAsync(request, cancellationToken);
            var httpApp = ((System.Web.HttpContextWrapper)request.Properties["MS_HttpContext"]).ApplicationInstance;

            string filePath = request.RequestUri.AbsolutePath;
            string fileName = VirtualPathUtility.GetFileName(filePath);
            
            var routeName = fileName.IsNullOrWhiteSpace().IfElse(
                () =>
                {
                    var path = filePath.Split(new char[] { '/' });
                    return path.Any() ? path.Last() : "";
                },
                () => fileName);

            if (!lookup.ContainsKey(routeName))
                return await base.SendAsync(request, cancellationToken);

            var possibleHttpMethods = lookup[routeName];
            var matchingKey = possibleHttpMethods
                .SelectKeys()
                .Where(key => key == request.Method);

            if (!matchingKey.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            var httpResponseMessage = await CreateResponseAsync(httpApp, request, possibleHttpMethods[matchingKey.First()]);
            //context.Response.

            return httpResponseMessage;
        }

        //public void Dispose()
        //{
        //    //throw new NotImplementedException();
        //}

        public void Init(HttpApplication context)
        {
            var wrapper = new EventHandlerTaskAsyncHelper(RouteToControllerAsync);
            context.AddOnBeginRequestAsync(wrapper.BeginEventHandler, wrapper.EndEventHandler);

        }

        private async Task RouteToControllerAsync(object sender, EventArgs e)
        {
            var httpApp = (HttpApplication)sender;
            var context = httpApp.Context;
            string filePath = context.Request.FilePath;
            string fileName = VirtualPathUtility.GetFileName(filePath);
            
            if (lookup.IsDefault())
                LocateControllers();

            var routeName = fileName.IsNullOrWhiteSpace().IfElse(
                () =>
                {
                    var path = filePath.Split(new char[] { '/' });
                    return path.Any() ? path.Last() : "";
                },
                () => fileName);

            if (!lookup.ContainsKey(routeName))
                return;

            var possibleHttpMethods = lookup[routeName];
            var matchingKey = possibleHttpMethods
                .SelectKeys()
                .Where(key => key.Method == context.Request.HttpMethod);

            if (!matchingKey.Any())
                context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;

            if (!HttpContext.Current.Items.Contains("MS_HttpRequestMessage"))
                return;

            var httpRequestMessage = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), context.Request.Url); // HttpContext.Current.Items["MS_HttpRequestMessage"] as HttpRequestMessage;
            var httpResponseMessage = await CreateResponseAsync(httpApp, httpRequestMessage, possibleHttpMethods[matchingKey.First()]);
            //context.Response.

            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
        
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
                { typeof(EastFive.Api.HttpOptionsAttribute), HttpMethod.Options },
            };

        private void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly
                    .GetTypes();
                var results = types
                    .Where(type => type.IsClass && type.ContainsCustomAttribute<FunctionViewControllerAttribute>())
                    .Select(
                        (type) =>
                        {
                            var attr = type.GetCustomAttribute<FunctionViewControllerAttribute>();
                            IDictionary<HttpMethod, MethodInfo[]> methods = methodLookup
                                .Select(
                                    methodKvp => methodKvp.Value.PairWithValue(
                                        type
                                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                            .Where(method => method.ContainsCustomAttribute(methodKvp.Key))
                                    .ToArray()))
                                .ToDictionary();
                            return attr.Route.IfThen(attr.Route.IsNullOrWhiteSpace(),
                                (route) => type.Name).PairWithValue(methods);
                        })
                    .ToArray();

                this.lookup = this.lookup.NullToEmpty().Concat(results).ToDictionary();
            } catch (Exception ex)
            {
                ex.GetType();
            }
        }

        private async Task<HttpResponseMessage> CreateResponseAsync(HttpApplication httpApp, HttpRequestMessage request, MethodInfo[] methods)
        {
            var queryParams = request.RequestUri.ParseQuery()
                .Select(kvp => kvp.Key.ToLower().PairWithValue<string, Func<Type, Task<object>>>(type => ControllerExtensions.StringContentToType(type, kvp.Value).ToTask()))
                .Concat(await request.Content.ParseOptionalMultipartValuesAsync())
                .SelectPartition(
                    (param, plain, dictionary) => param.Key.MatchRegex<KeyValuePair<string, string>, Dictionary<string, Func<Type, Task<object>>>>(
                        @"(?<Key>[a-zA-Z0-9]+)\[(?<EAN>[a-zA-Z0-9]+)\]",
                        (kvp) => dictionary(kvp.Key.PairWithValue(kvp.Value.PairWithValue(param.Value))),
                        () => plain(param),
                        kvp => kvp.Key,
                        kvp => kvp.Value),
                    (KeyValuePair<string, Func<Type, Task<object>>> [] plains, KeyValuePair<string, KeyValuePair<string, Func<Type, Task<object>>>>[] dictionaries) =>
                    {
                        return plains.ToDictionary();
                        //.Concat(
                        //    dictionaries
                        //        .GroupBy(kvp => kvp.Key)
                        //        .Select(grp => grp.Key
                        //        kvp =>  ,
                        //    )
                        //    .ToDictionary();
                    });

            var response = await methods
                .SelectPartition(
                    (method, removeParams, addParams) =>
                    {
                        return method
                            .GetParameters()
                            .SelectPartitionOptimized(
                                (param, validated, unvalidated) =>
                                    param.ContainsCustomAttribute<QueryValidationAttribute>() ?
                                        validated(param)
                                        :
                                        unvalidated(param),
                                (ParameterInfo[] parametersRequiringValidation, ParameterInfo[] parametersNotRequiringValidation) =>
                                {
                                    return parametersRequiringValidation.SelectPartition(
                                        async (parameterRequiringValidation, validValue, group2) =>
                                        {
                                            var validator = parameterRequiringValidation.GetCustomAttribute<QueryValidationAttribute>();
                                            if(queryParams.ContainsKey(parameterRequiringValidation.Name.ToLower()))
                                                return await await validator.TryCastAsync(httpApp, request, parameterRequiringValidation,
                                                    async (type, success, failure) => success(await queryParams[parameterRequiringValidation.Name.ToLower()](type)),
                                                    v => validValue(parameterRequiringValidation.PairWithValue(v)),
                                                    (why) => group2(parameterRequiringValidation.PairWithValue(why)));

                                            return await await validator.OnEmptyValueAsync(httpApp, request, parameterRequiringValidation,
                                                v => validValue(parameterRequiringValidation.PairWithValue(v)),
                                                () => group2(parameterRequiringValidation.PairWithValue("Value not provided")));
                                        },
                                        async (KeyValuePair<ParameterInfo, object>[] parametersRequiringValidationWithValues, KeyValuePair<ParameterInfo, string>[] parametersRequiringValidationThatDidNotValidate) =>
                                        {
                                            if (parametersRequiringValidationThatDidNotValidate.Any())
                                                return await addParams(parametersRequiringValidationThatDidNotValidate);

                                            var parametersNotRequiringValidationWithValues = await parametersNotRequiringValidation
                                                .Where(unvalidatedParam => queryParams.ContainsKey(unvalidatedParam.Name.ToLower()))
                                                .Select(async unvalidatedParam => unvalidatedParam.PairWithValue(await queryParams[unvalidatedParam.Name.ToLower()](unvalidatedParam.ParameterType)))
                                                .WhenAllAsync();

                                            var parametersWithValues = parametersNotRequiringValidationWithValues
                                                .Concat(parametersRequiringValidationWithValues)
                                                .ToArray();

                                            #region Check for extra parameters that did not match anything

                                            var matchedParamsLookup = parametersRequiringValidation.Concat(parametersNotRequiringValidation).Select(pi => pi.Name.ToLower()).AsHashSet();
                                            var extraParams = queryParams.SelectKeys().Except(matchedParamsLookup).ToArray();
                                            if (extraParams.Any())
                                                return await removeParams(extraParams);

                                            #endregion

                                            return await InvokeValidatedMethod(httpApp, request, method, parametersWithValues,
                                                (missingParams) => addParams(missingParams.Select(param => param.PairWithValue("Missing")).ToArray()));
                                        });
                                });
                    },
                    (string[][] removeParams, KeyValuePair<ParameterInfo, string>[][] addParams) =>
                    {
                        var content =
                            addParams.Any() ? $"Please correct the value for [{addParams.Select(uvs => uvs.Select(uv => $"{uv.Key.Name} ({uv.Value})").Join(",")).Join(" or ")}]." : "" +
                            (removeParams.Any() ? $"Remove query parameters [{removeParams.Select(uvs => uvs.Select(uv => uv).Join(",")).Join(" or ")}]" : "");
                        return request
                            .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                            .AddReason(content)
                            .ToTask();
                    });
            return response;
        }

        private Task<HttpResponseMessage> InvokeValidatedMethod(HttpApplication httpApp, HttpRequestMessage request, MethodInfo method, 
            KeyValuePair<ParameterInfo, object>[] queryParameters,
            Func<ParameterInfo[], Task<HttpResponseMessage>> onMissingParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (methodParameter, next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        if (ControllerModule.instigators.ContainsKey(methodParameter.ParameterType))
                            return await ControllerModule.instigators[methodParameter.ParameterType](httpApp, request, methodParameter, 
                                (v) => next(v));

                        return request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                            .AddReason($"Could not instigate type: {methodParameter.ParameterType.FullName}. Please add an instigator for that type.");
                    },
                    async (object[] methodParameters) =>
                    {
                        var response = method.Invoke(null, methodParameters);
                        if (typeof(HttpResponseMessage).IsAssignableFrom(method.ReturnType))
                            return ((HttpResponseMessage)response);
                        if (typeof(Task<HttpResponseMessage>).IsAssignableFrom(method.ReturnType))
                            return (await (Task<HttpResponseMessage>)response);
                        if (typeof(Task<Task<HttpResponseMessage>>).IsAssignableFrom(method.ReturnType))
                            return (await await (Task<Task<HttpResponseMessage>>)response);

                        return (request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                            .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
                    });
        }
        
        public delegate Task<HttpResponseMessage> InstigatorDelegate(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);
        
        public static Dictionary<Type, InstigatorDelegate> instigators =
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
                        Controllers.ContentResponse dele = (obj) => request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
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
                    typeof(EastFive.Api.Controllers.ViewFileResponse),
                    (httpApp, request, paramInfo, success) =>
                    {
                        EastFive.Api.Controllers.ViewFileResponse dele =
                            (viewPath, content) =>
                            {
                                try
                                {
                                    var viewContent = System.IO.File.OpenText($"{HttpRuntime.AppDomainAppPath}Views\\{viewPath}").ReadToEnd();
                                    var response = request.CreateResponse(HttpStatusCode.OK);
                                    var parsedView =  RazorEngine.Razor.Parse(viewContent, content);
                                    response.Content = new StringContent(parsedView);
                                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
                                    return response;
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

        public static void AddInstigator(Type type, InstigatorDelegate instigator)
        {
            instigators.Add(type, instigator);
        }
    }
}
