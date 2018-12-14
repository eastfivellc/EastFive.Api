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
using System.IO;
using EastFive.Linq.Async;

namespace EastFive.Api.Modules
{
    public class ControllerHandler : ApplicationHandler
    {
        public ControllerHandler(System.Web.Http.HttpConfiguration config)
            : base(config)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpApplication httpApp, 
            HttpRequestMessage request, CancellationToken cancellationToken, 
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation)
        {
            return DirectSendAsync(httpApp, request, cancellationToken, continuation);
        }

        /// <summary>
        /// This method is available if an external system or test needs to invoke the routing.
        /// SendAsync serves as the MVC.API Binding to this method.
        /// </summary>
        /// <param name="httpApp"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="continuation"></param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> DirectSendAsync(HttpApplication httpApp,
            HttpRequestMessage request, CancellationToken cancellationToken,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation)
        {
            string filePath = request.RequestUri.AbsolutePath;
            var path = filePath.Split(new char[] { '/' }).Where(pathPart => !pathPart.IsNullOrWhiteSpace()).ToArray();
            var routeName = (path.Length >= 2 ? path[1] : "").ToLower();

            return await httpApp.GetControllerMethods(routeName,
                async (possibleHttpMethods) =>
                {
                    var matchingKey = possibleHttpMethods
                        .SelectKeys()
                        .Where(key => String.Compare(key.Method, request.Method.Method, true) == 0);

                    if (!matchingKey.Any())
                        return request.CreateResponse(HttpStatusCode.NotImplemented);

                    var httpMethod = matchingKey.First();
                    return await CreateResponseAsync(httpApp, request, routeName, possibleHttpMethods[httpMethod]);
                },
                () => continuation(request, cancellationToken));
        }

        #region Invoke correct method

        delegate TResult ParseContentDelegate<TResult>(Type type, Func<object, TResult> onParsed, Func<string, TResult> onFailure);

        private class QueryParamTokenParser : IParseToken
        {
            private string value;

            public QueryParamTokenParser(string value)
            {
                this.value = value;
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

            public string ReadString()
            {
                return value;
            }
        }

        private static async Task<HttpResponseMessage> CreateResponseAsync(HttpApplication httpApp, HttpRequestMessage request, string controllerName, MethodInfo[] methods)
        {
            #region setup query parameter casting

            var queryParameters = request.RequestUri.ParseQuery()
                .Select(kvp => kvp.Key.ToLower().PairWithValue(kvp.Value))
                .ToDictionary();
            var queryParameterCollections = GetCollectionParameters(httpApp, queryParameters).ToDictionary();
            CastDelegate<SelectParameterResult> queryCastDelegate =
                (query, type, onParsed, onFailure) =>
                {
                    var queryKey = query.ToLower();
                    if (!queryParameters.ContainsKey(queryKey))
                    {
                        if(!queryParameterCollections.ContainsKey(queryKey))
                            return onFailure($"Missing query parameter `{queryKey}`").AsTask();
                        return queryParameterCollections[queryKey](
                                type,
                                vs => onParsed(vs),
                                why => onFailure(why))
                            .AsTask();
                    }
                    var queryValue = new QueryParamTokenParser(queryParameters[queryKey]);
                    return httpApp
                        .Bind(type, queryValue,
                            v => onParsed(v),
                            (why) => onFailure(why))
                        .AsTask();
                };

            #endregion

            #region Get file name from URI (optional part between the controller name and the query)

            CastDelegate<SelectParameterResult> fileNameCastDelegate =
                (query, type, onParsed, onFailure) =>
                {
                    return request.RequestUri.AbsoluteUri
                        .MatchRegexInvoke(
                            $".*/(?i){controllerName}(?-i)/(?<defaultQueryParam>[a-zA-Z0-9-]+)",
                            defaultQueryParam => defaultQueryParam,
                            (string[] urlFileNames) =>
                            {
                                if (!urlFileNames.Any())
                                    return onFailure("No URI filename value provided.");
                                return httpApp.Bind(type,
                                        new QueryParamTokenParser(urlFileNames.First()),
                                    v => onParsed(v),
                                    (why) => onFailure(why));
                            })
                        .AsTask();
                };

            #endregion

            return await httpApp.ParseContentValuesAsync<SelectParameterResult, HttpResponseMessage>(request.Content,
                async (bodyParser, bodyValues) =>
                {
                    CastDelegate<SelectParameterResult> bodyCastDelegate =
                        (queryKey, type, onParsed, onFailure) =>
                        {
                            return bodyParser(queryKey, type,
                                value =>
                                {
                                    var parsedResult = onParsed(value);
                                    return parsedResult;
                                },
                                (why) => onFailure(why));
                        };
                    return await GetResponseAsync(methods,
                        queryCastDelegate,
                        bodyCastDelegate,
                        fileNameCastDelegate,
                        httpApp, request,
                        queryParameters.SelectKeys(),
                        bodyValues);
                });
            
        }

        #region Generate collection parameters
        
        private struct MultipartParameter
        {
            public string index;
            public string key;
            public Func<Type, Func<object, object>, Func<string, object>, object> fetchValue;
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

        private static IEnumerable<KeyValuePair<string, ParseContentDelegate<SelectParameterResult>>> GetCollectionParameters(HttpApplication httpApp, IEnumerable<KeyValuePair<string, string>> queryParameters)
        {
            // Convert parameters into Collections if necessary
            return queryParameters
                .SelectOptional<KeyValuePair<string, string>, MultipartParameter>(
                    (param, select, skip) => param.Key.MatchRegexInvoke(
                        @"(?<key>[a-zA-Z0-9]+)\[(?<value>[a-zA-Z0-9]+)\]",
                        (string key, string value) => new KeyValuePair<string, string>(key, value),
                        (kvps) =>
                        {
                            if (!kvps.Any())
                                return skip();

                            var kvp = kvps.First();
                            var multipartParam = new MultipartParameter
                            {
                                index = kvp.Key,
                                key = kvp.Value,
                                fetchValue = (type, onSuccess, onFailure) =>
                                    httpApp
                                        .Bind(type, new QueryParamTokenParser(param.Value),
                                            v => onSuccess(v),
                                            why => onFailure(why))
                                        .AsTask(),
                            };
                            return select(multipartParam);
                        }))
                .GroupBy(collectionParameter => collectionParameter.index)
                .Select(
                    collectionParameterGrp =>
                        collectionParameterGrp.Key.ToLower()
                            .PairWithValue<string, ParseContentDelegate<SelectParameterResult>>(
                                (collectionType, onParsed, onFailure) =>
                                {
                                    if (collectionType.IsGenericType)
                                    {
                                        var genericArgs = collectionType.GenericTypeArguments;
                                        if (genericArgs.Length == 1)
                                        {
                                            // It's an array
                                            var typeToCast = genericArgs.First();
                                            var lookup = collectionParameterGrp
                                                .SelectOptional<MultipartParameter, object>(
                                                    (collectionParameter, next, skip) => (EastFive.Linq.EnumerableExtensions.ISelected<object>)collectionParameter.fetchValue(typeToCast,
                                                        v => next(v),
                                                        (why) => skip()))
                                                .ToArray();
                                            return onParsed(lookup);
                                        }
                                        if (genericArgs.Length == 2)
                                        {
                                            // It's an dictionary
                                            var typeToCast = genericArgs[1];
                                            var kvpCreateMethod = typeof(ControllerHandler).GetMethod("KvpCreate", BindingFlags.Static | BindingFlags.NonPublic);
                                            var correctGenericKvpCreate = kvpCreateMethod.MakeGenericMethod(genericArgs);
                                            var lookup = collectionParameterGrp
                                                .FlatMap(
                                                    (collectionParameter, next, skip) =>
                                                        (object[])collectionParameter.fetchValue(typeToCast,
                                                            v => next(correctGenericKvpCreate.Invoke(null, new object[] { collectionParameter.key, v })),
                                                            (why) => skip()),
                                                    (IEnumerable<object> lookupInner) => lookupInner.ToArray());

                                            var castMethod = typeof(ControllerHandler).GetMethod("CastToKvp", BindingFlags.Static | BindingFlags.NonPublic);
                                            var correctKvpsCast = castMethod.MakeGenericMethod(genericArgs);
                                            var kvpsOfCorrectTypes = correctKvpsCast.Invoke(null, lookup.AsArray());

                                            var dictCreateMethod = typeof(ControllerHandler).GetMethod("DictionaryCreate", BindingFlags.Static | BindingFlags.NonPublic);
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
                                        var values = collectionParameterGrp
                                            .FlatMap(
                                                (collectionParameter, next, skip) => (IEnumerable<object>)collectionParameter.fetchValue(typeToCast, v => next(v), (why) => skip()),
                                                (IEnumerable<object> lookup) => lookup);
                                        return onParsed(values);
                                    }
                                    if (typeof(System.Collections.DictionaryBase).IsAssignableFrom(collectionType))
                                    {
                                        // It's an dictionary
                                        var typeToCast = typeof(object);
                                        var dictionary = collectionParameterGrp
                                            .FlatMap(
                                                (collectionParameter, next, skip) => (Dictionary<string, object>)collectionParameter.fetchValue(typeToCast,
                                                    v => next(collectionParameter.key.PairWithValue(v)),
                                                    (why) => skip()),
                                                (IEnumerable<KeyValuePair<string, object>> lookups) => lookups.ToDictionary());
                                        return onParsed(dictionary);
                                    }
                                    return onFailure($"Cannot parse collection of type {collectionType.FullName}");
                                }));
        }

        #endregion

        private struct MethodCast
        {
            public bool valid;
            public string[] extraBodyParams;
            public string[] extraQueryParams;
            public SelectParameterResult[] failedValidations;
            public MethodInfo method;
            internal KeyValuePair<ParameterInfo, object>[] parametersWithValues;

            public string ErrorMessage
            {
                get
                {
                    var failedValidationErrorMessages = failedValidations
                        .Select(
                            paramResult =>
                            {
                                var validator = paramResult.parameterInfo.GetCustomAttribute<QueryValidationAttribute>();
                                var lookupName = validator.Name.IsNullOrWhiteSpace() ?
                                    paramResult.parameterInfo.Name.ToLower()
                                    :
                                    validator.Name.ToLower();
                                var location = paramResult.fromQuery ?
                                    "Query"
                                    :
                                    paramResult.fromBody ?
                                        "BODY"
                                        :
                                        string.Empty;
                                return $"{lookupName}({location}):{paramResult.failure}";
                            })
                        .ToArray();
                    
                    var contentFailedValidations = failedValidationErrorMessages.Any() ?
                        $"Please correct the values for [{failedValidationErrorMessages.Join(",")}]"
                        :
                        "";

                    var extraParamMessages = extraQueryParams
                        .NullToEmpty()
                        .Select(extraQueryParam => $"{extraQueryParam}(QUERY)")
                        .Concat(
                            extraBodyParams
                                .NullToEmpty()
                                .Select(extraBodyParam => $"{extraBodyParam}(BODY)"));
                    var contentExtraParams = extraParamMessages.Any() ?
                        $"emove parameters [{extraParamMessages.Join(",")}]."
                        :
                        "";

                    if(contentFailedValidations.IsNullOrWhiteSpace())
                    {
                        if (contentExtraParams.IsNullOrWhiteSpace())
                            return "Query validation failure";

                        return $"R{contentExtraParams}";
                    }

                    if (contentExtraParams.IsNullOrWhiteSpace())
                        return contentFailedValidations;

                    return $"{contentFailedValidations} and r{contentExtraParams}";
                }
            }
        }

        private static async Task<HttpResponseMessage> GetResponseAsync(MethodInfo[] methods,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchNameParam,
            HttpApplication httpApp, HttpRequestMessage request,
            IEnumerable<string> queryKeys, IEnumerable<string> bodyKeys)
        {
            var methodsForConsideration = methods
                .Select(
                    async (method) =>
                    {
                        var parametersCastResults = await method
                            .GetParameters()
                            .Where(param => param.ContainsCustomAttribute<QueryValidationAttribute>())
                            .Select(
                                (param) =>
                                {
                                    var castValue = param.GetCustomAttribute<QueryValidationAttribute, IProvideApiValue>(
                                        (queryValidationAttribute) => queryValidationAttribute,
                                        () => throw new Exception("Where check failed"));
                                    return castValue.TryCastAsync(httpApp, request, method, param,
                                            fetchQueryParam,
                                            fetchBodyParam,
                                            fetchNameParam);
                                })
                            .WhenAllAsync();

                        var failedValidations = parametersCastResults
                            .Where(pcr => !pcr.valid)
                            .ToArray();
                        return HasExtraParameters(method,
                                queryKeys,
                                bodyKeys,
                                parametersCastResults,
                            () =>
                            {
                                if (failedValidations.Any())
                                {
                                    return new MethodCast
                                    {
                                        valid = false,
                                        failedValidations = failedValidations,
                                        method = method,
                                    };
                                }
                                var parametersWithValues = parametersCastResults
                                    .Select(parametersCastResult =>
                                        parametersCastResult.parameterInfo.PairWithValue(parametersCastResult.value))
                                    .ToArray();
                                return new MethodCast
                                {
                                    valid = false,
                                    method = method,
                                    parametersWithValues = parametersWithValues,
                                };
                            },
                            (extraParams) =>
                            {
                                return new MethodCast
                                {
                                    valid = true,
                                    failedValidations = failedValidations,
                                    method = method,
                                    extraBodyParams = extraParams,
                                };
                            });
                    })
                .AsyncEnumerable();
            return await await methodsForConsideration
                .Where(methodCast => !methodCast.parametersWithValues.IsDefaultOrNull())
                .FirstAsync(
                    (methodCast) =>
                    {
                        return InvokeValidatedMethod(httpApp, request, methodCast.method, methodCast.parametersWithValues,
                            (failedParameters) =>
                            {
                                methodCast.failedValidations = failedParameters;
                                var methodCasts = methodsForConsideration
                                    .Where(mc => mc.parametersWithValues.IsDefaultOrNull())
                                    .Append(methodCast);
                                return Issues(methodCasts);
                            });
                    },
                    () =>
                    {
                        return Issues(methodsForConsideration);
                    });

            async Task<HttpResponseMessage> Issues(IEnumerableAsync<MethodCast> methodsCasts)
            {
                var reasonStrings = await methodsCasts
                    .Select(
                        methodCast =>
                        {
                            var errorMessage = methodCast.ErrorMessage;
                            return errorMessage;
                        })
                    .ToArrayAsync();
                var content = reasonStrings.Join(";");
                return request
                    .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                    .AddReason(content);
            }
        }

        private static TResult HasExtraParameters<TResult>(MethodInfo method,
                IEnumerable<string> queryKeys, IEnumerable<string> bodyKeys,
                IEnumerable<SelectParameterResult> matchedParameters,
            Func<TResult> noExtraParameters,
            Func<string[], TResult> onExtraParams)
        {
            return method.GetCustomAttribute<HttpVerbAttribute, TResult>(
                verbAttr =>
                {
                    if (!verbAttr.MatchAllParameters)
                        return noExtraParameters();
                    
                    if (verbAttr.MatchAllQueryParameters)
                    {
                        var matchParamQueryLookup = matchedParameters
                            .Where(param => param.fromQuery)
                            .ToLookup(param => param.key);
                        var extraQueryKeys = queryKeys
                            .Where(queryKey => !matchParamQueryLookup.Contains(queryKey))
                            .ToArray();
                        if (extraQueryKeys.Any())
                            return onExtraParams(extraQueryKeys);
                    }

                    if (verbAttr.MatchAllBodyParameters)
                    {
                        var matchBodyLookup = matchedParameters
                            .Where(param => param.fromBody)
                            .ToLookup(param => param.key);
                        var extraQueryKeys = bodyKeys
                            .Where(queryKey => !matchBodyLookup.Contains(queryKey))
                            .ToArray();
                        if (extraQueryKeys.Any())
                            return onExtraParams(extraQueryKeys);
                    }
                    
                    return noExtraParameters();
                },
                () => throw new ArgumentException("method", $"Method {method.DeclaringType.FullName}..{method.Name}(...) does not have an HttpVerbAttribute."));
        }

        private static Task<HttpResponseMessage> InvokeValidatedMethod(HttpApplication httpApp, HttpRequestMessage request, MethodInfo method, 
            KeyValuePair<ParameterInfo, object>[] queryParameters,
            Func<SelectParameterResult[], Task<HttpResponseMessage>> onMissingParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (methodParameter, next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(request, methodParameter,
                            v => next(v));
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
                            if (method.IsGenericMethod)
                            {
                                var genericArguments = method.GetGenericArguments().Select(arg => arg.Name).Join(",");
                                return request.CreateResponse(HttpStatusCode.InternalServerError)
                                    .AddReason($"Could not invoke {method.DeclaringType.FullName}..{method.Name} because it contains generic arguments:{genericArguments}");
                            }

                            var response = method.Invoke(null, methodParameters);
                            if (typeof(HttpResponseMessage).IsAssignableFrom(method.ReturnType))
                                return ((HttpResponseMessage)response);
                            if (typeof(Task<HttpResponseMessage>).IsAssignableFrom(method.ReturnType))
                                return (await (Task<HttpResponseMessage>)response);
                            if (typeof(Task<Task<HttpResponseMessage>>).IsAssignableFrom(method.ReturnType))
                                return (await await (Task<Task<HttpResponseMessage>>)response);
                        }
                        catch (TargetInvocationException ex)
                        {
                            var paramList = methodParameters.Select(p => p.GetType().FullName).Join(",");
                            var body = ex.InnerException.IsDefaultOrNull() ?
                                ex.StackTrace
                                :
                                $"[{ex.InnerException.GetType().FullName}]{ex.InnerException.Message}:\n{ex.InnerException.StackTrace}";
                            return request
                                .CreateResponse(HttpStatusCode.InternalServerError, body)
                                .AddReason($"Could not invoke {method.DeclaringType.FullName}.{method.Name}({paramList})");
                        }
                        catch (Exception ex)
                        {
                            // TODO: Only do this in a development environment
                            return request.CreateResponse(HttpStatusCode.InternalServerError, ex.StackTrace).AddReason(ex.Message);
                        }
                        
                        return (request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                            .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
                    });
        }
        
        #endregion

        

    }
}
