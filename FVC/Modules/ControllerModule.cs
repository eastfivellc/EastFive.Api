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
            if (!request.Properties.ContainsKey("MS_HttpContext"))
                return await base.SendAsync(request, cancellationToken);
            var httpApp = ((System.Web.HttpContextWrapper)request.Properties["MS_HttpContext"]).ApplicationInstance;

            string filePath = request.RequestUri.AbsolutePath;
            var path = filePath.Split(new char[] { '/' }).Where(pathPart => !pathPart.IsNullOrWhiteSpace()).ToArray();
            var routeName =  (path.Length >= 2 ? path[1] : "").ToLower();
            
            if (!lookup.ContainsKey(routeName))
                return await base.SendAsync(request, cancellationToken);

            var possibleHttpMethods = lookup[routeName];
            var matchingKey = possibleHttpMethods
                .SelectKeys()
                .Where(key => key == request.Method);

            if (!matchingKey.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            var controllerName = matchingKey.First();
            var httpResponseMessage = await CreateResponseAsync(httpApp, request, routeName, possibleHttpMethods[controllerName]);

            return httpResponseMessage;
        }

        #region Load Controllers

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
                            return attr.Route
                                .IfThen(attr.Route.IsNullOrWhiteSpace(),
                                    (route) => type.Name)
                                .ToLower()
                                .PairWithValue(methods);
                        })
                    .ToArray();

                this.lookup = this.lookup.NullToEmpty().Concat(results).ToDictionary();
            } catch (Exception ex)
            {
                ex.GetType();
            }
        }
        
        #endregion

        #region Invoke correct method

        private struct MultipartParameter
        {
            public string index;
            public string key;
            public ParseContentDelegate<object[]> fetchValue;
        }

        private static KeyValuePair<TKey, TValue> KvpCreate<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }

        private static IDictionary<TKey, TValue> DictionaryCreate<TKey, TValue>(KeyValuePair<TKey, TValue>[] kvps)
        {
            return kvps.ToDictionary();
        }

        private static KeyValuePair<TKey, TValue>[] CastToKvp<TKey, TValue>(IEnumerable<object> objs)
        {
            return objs.Cast<KeyValuePair<TKey, TValue>>().ToArray();
        }

        private const string defaultKeyPlaceholder = "__DEFAULT_ID__";

        delegate TResult ParseContentDelegate<TResult>(Type type, Func<object, TResult> onParsed, Func<string, TResult> onFailure);

        private async Task<HttpResponseMessage> CreateResponseAsync(HttpApplication httpApp, HttpRequestMessage request, string controllerName, MethodInfo[] methods)
        {
            var allParamInvokators =
                // Query parameters from URI
                request.RequestUri.ParseQuery()
                .Select(kvp => kvp.Key.ToLower().PairWithValue<string, ParseContentDelegate<object[]>>(
                    (type, onParsed, onFailure) => ControllerExtensions.StringContentToType(type, kvp.Value, v => onParsed(v), (why) => onFailure(why))))

                // File name from URI
                .If(true,
                    queryParamsFromUri => request.RequestUri.AbsoluteUri.MatchRegexInvoke($".*/(?i){controllerName}(?-i)/(?<defaultQueryParam>[a-zA-Z0-9-]+)",
                        defaultQueryParam => queryParamsFromUri
                                .Append(defaultKeyPlaceholder.PairWithValue<string, ParseContentDelegate<object[]>>(
                                    (type, onParsed, onFailure) => ControllerExtensions.StringContentToType(type,
                                        defaultQueryParam, v => onParsed(v),
                                        (why) => onFailure(why)))),
                        updates => updates.Any()? updates.First() : queryParamsFromUri))
                
                // Body parameters
                .Concat((await request.Content.ParseContentValuesAsync())
                    .Select(
                        parser =>
                        {
                            ParseContentDelegate<object[]> callback = (type, onParsed, onFailure) =>
                            {
                                return onParsed(parser.Value(type));
                            };
                            return parser.Key.PairWithValue(callback);
                        }))
                
                // Convert parameters into Collections if necessary
                .SelectPartition(
                    (param, plain, dictionary) => param.Key.MatchRegexInvoke(
                        @"(?<key>[a-zA-Z0-9]+)\[(?<value>[a-zA-Z0-9]+)\]",
                        (string key, string value) => new KeyValuePair<string, string>(key, value),
                        (kvps) =>
                        {
                            if (!kvps.Any())
                                return plain(param);

                            var kvp = kvps.First();
                            var multipartParam = new MultipartParameter
                            {
                                index = kvp.Key,
                                key = kvp.Value,
                                fetchValue = param.Value,
                            };
                            return dictionary(multipartParam);
                        }),
                    (KeyValuePair<string, ParseContentDelegate<object[]>> [] plains, MultipartParameter[] collectionParameters) =>
                    {
                        var options = collectionParameters
                            .GroupBy(collectionParameter => collectionParameter.index)
                            .Select(
                                collectionParameterGrp =>
                                    collectionParameterGrp.Key.ToLower().PairWithValue<string, ParseContentDelegate<object[]>>(
                                        (collectionType, onParsed, onFailure) =>
                                        {
                                            if(collectionType.IsGenericType)
                                            {
                                                var genericArgs = collectionType.GenericTypeArguments;
                                                if (genericArgs.Length == 1)
                                                {
                                                    // It's an array
                                                    var typeToCast = genericArgs.First();
                                                    return collectionParameterGrp
                                                        .FlatMap(
                                                            (collectionParameter, next, skip) => collectionParameter.fetchValue(typeToCast, v => next(v), (why) => skip()),
                                                            (IEnumerable<object> lookup) => lookup.ToArray());
                                                }
                                                if (genericArgs.Length == 2)
                                                {
                                                    // It's an dictionary
                                                    var typeToCast = genericArgs[1];
                                                    var kvpCreateMethod = typeof(ControllerModule).GetMethod("KvpCreate", BindingFlags.Static | BindingFlags.NonPublic);
                                                    var correctGenericKvpCreate = kvpCreateMethod.MakeGenericMethod(genericArgs);
                                                    var lookup = collectionParameterGrp
                                                        .FlatMap(
                                                            (collectionParameter, next, skip) =>
                                                                collectionParameter.fetchValue(typeToCast,
                                                                    v => next(correctGenericKvpCreate.Invoke(null, new object[] { collectionParameter.key, v } )),
                                                                    (why) => skip()),
                                                            (IEnumerable<object> lookupInner) => lookupInner.ToArray());

                                                    var castMethod = typeof(ControllerModule).GetMethod("CastToKvp", BindingFlags.Static | BindingFlags.NonPublic);
                                                    var correctKvpsCast = castMethod.MakeGenericMethod(genericArgs);
                                                    var kvpsOfCorrectTypes = correctKvpsCast.Invoke(null, lookup.AsArray());

                                                    var dictCreateMethod = typeof(ControllerModule).GetMethod("DictionaryCreate", BindingFlags.Static | BindingFlags.NonPublic);
                                                    var correctGenericDictCreate = dictCreateMethod.MakeGenericMethod(genericArgs);
                                                    var dictionaryOfCorrectTypes = correctGenericDictCreate.Invoke(null, kvpsOfCorrectTypes.AsArray());
                                                    return onParsed(dictionaryOfCorrectTypes);
                                                }
                                                return onFailure($"Cannot parse collection of type {collectionType.FullName}");
                                            }
                                            if (typeof(Enumerable).IsAssignableFrom(collectionType))
                                            {
                                                // It's an array
                                                var typeToCast = typeof(object);
                                                return collectionParameterGrp
                                                    .FlatMap(
                                                        (collectionParameter, next, skip) => collectionParameter.fetchValue(typeToCast, v => next(v), (why) => skip()),
                                                        (IEnumerable<object> lookup) => onParsed(lookup.ToArray()));
                                            }
                                            if (typeof(System.Collections.DictionaryBase).IsAssignableFrom(collectionType))
                                            {
                                                // It's an dictionary
                                                var typeToCast = typeof(object);
                                                return collectionParameterGrp
                                                    .FlatMap(
                                                        (collectionParameter, next, skip) => collectionParameter.fetchValue(typeToCast, 
                                                            v => next(collectionParameter.key.PairWithValue(v)),
                                                            (why) => skip()),
                                                        (IEnumerable<KeyValuePair<string, object>> lookups) => onParsed(lookups.ToDictionary()));
                                            }
                                            return onFailure($"Cannot parse collection of type {collectionType.FullName}");
                                        }));

                        return plains.Concat(options).ToArray();
                    });


            var duplicates = allParamInvokators.SelectKeys().Duplicates((s1, s2) => s1 == s2);
            if (duplicates.Any())
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason($"Conflicting query and body parameters for: [{duplicates.Join(" and ")}]");
            var queryParams = allParamInvokators.ToDictionary();

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
                                        async (parameterRequiringValidation, validValue, didNotValidate) =>
                                        {
                                            var validator = parameterRequiringValidation.GetCustomAttribute<QueryValidationAttribute>();
                                            var lookupName = validator.Name.IsNullOrWhiteSpace() ? parameterRequiringValidation.Name.ToLower() : validator.Name.ToLower();

                                            // Handle default params
                                            if(!queryParams.ContainsKey(lookupName))
                                                lookupName = parameterRequiringValidation.GetCustomAttribute<QueryDefaultParameterAttribute, string>(
                                                    defaultAttr => defaultKeyPlaceholder,
                                                    () => lookupName);

                                            if (queryParams.ContainsKey(lookupName))
                                                return await await validator.TryCastAsync(httpApp, request, method, parameterRequiringValidation,
                                                        async (type, success, failure) =>
                                                        {
                                                            // Hack here
                                                            var strArray = queryParams[lookupName](type,
                                                                (value) => value.AsArray(),
                                                                (why) => new object[] { "", why });
                                                            if (strArray.Length == 1)
                                                                return success(strArray[0]);
                                                            return failure((string)strArray[1]);
                                                        },
                                                    v => validValue(parameterRequiringValidation.PairWithValue(v)),
                                                    (why) => didNotValidate(parameterRequiringValidation.PairWithValue(why)));
                                            
                                            return await await validator.OnEmptyValueAsync(httpApp, request, parameterRequiringValidation,
                                                v => validValue(parameterRequiringValidation.PairWithValue(v)),
                                                () => didNotValidate(parameterRequiringValidation.PairWithValue("Value not provided")));
                                        },
                                        async (KeyValuePair<ParameterInfo, object>[] parametersRequiringValidationWithValues, KeyValuePair<ParameterInfo, string>[] parametersRequiringValidationThatDidNotValidate) =>
                                        {
                                            if (parametersRequiringValidationThatDidNotValidate.Any())
                                                return await addParams(parametersRequiringValidationThatDidNotValidate);

                                            var parametersNotRequiringValidationWithValues = parametersNotRequiringValidation
                                                .Where(unvalidatedParam => queryParams.ContainsKey(unvalidatedParam.Name.ToLower()))
                                                .Select(
                                                    (unvalidatedParam) =>
                                                    {
                                                        var queryParamValue = queryParams[unvalidatedParam.Name.ToLower()](unvalidatedParam.ParameterType,
                                                            v => v.AsArray(),
                                                            why => unvalidatedParam.ParameterType.GetDefault().AsArray()).First();
                                                        return unvalidatedParam.PairWithValue(queryParamValue);
                                                    });

                                            var parametersWithValues = parametersNotRequiringValidationWithValues
                                                .Concat(parametersRequiringValidationWithValues)
                                                .ToArray();

                                            return await HasExtraParameters(method,
                                                    parametersRequiringValidation.Concat(parametersNotRequiringValidation),
                                                    queryParams.SelectKeys(),
                                                () => InvokeValidatedMethod(httpApp, request, method, parametersWithValues,
                                                    (missingParams) => addParams(missingParams.Select(param => param.PairWithValue("Missing")).ToArray())),
                                                (extraParams) => removeParams(extraParams));
                                            
                                        });
                                });
                    },
                    (string[][] removeParams, KeyValuePair<ParameterInfo, string>[][] addParams) =>
                    {
                        var content =
                            (addParams.Any()    ? $"Please correct the value for [{addParams.Select(uvs => uvs.Select(uv => $"{uv.Key.Name} ({uv.Value})").Join(",")).Join(" or ")}]." : "")
                            +
                            (removeParams.Any() ? $"Remove query parameters [{  removeParams.Select(uvs => uvs.Select(uv => uv)                           .Join(",")).Join(" or ")}]." : "");
                        return request
                            .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                            .AddReason(content)
                            .ToTask();
                    });
            return response;
        }

        private TResult HasExtraParameters<TResult>(MethodInfo method, 
                IEnumerable<ParameterInfo> parameters, IEnumerable<string> queryKeys,
            Func<TResult> noExtraParameters,
            Func<string[], TResult> onExtraParams)
        {
            return method.GetCustomAttribute<HttpVerbAttribute, TResult>(
                verbAttr =>
                {
                    if (!verbAttr.MatchAllParameters)
                        return noExtraParameters();

                    var matchedParamsLookup = parameters
                        .Select(pi => pi.GetCustomAttribute<QueryValidationAttribute, string>(
                            validator => validator.Name.IsNullOrWhiteSpace() ? pi.Name.ToLower() : validator.Name.ToLower(),
                            () => pi.Name.ToLower()))
                            .AsHashSet();
                    var extraParams = queryKeys
                        .Where(key => key != defaultKeyPlaceholder)
                        .Except(matchedParamsLookup)
                        .ToArray();

                    if (extraParams.Any())
                        return onExtraParams(extraParams);

                    return noExtraParameters();
                },
                noExtraParameters);
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
        
        #endregion

        #region Instigators

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

        #endregion

    }
}
