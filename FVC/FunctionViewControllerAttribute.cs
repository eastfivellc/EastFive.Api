﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BlackBarLabs;
using EastFive.Api.Bindings;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;

namespace EastFive.Api
{
    public class FunctionViewControllerAttribute 
        : Attribute, IInvokeResource, IDocumentRoute
    {
        public string Namespace { get; set; }
        public string Route { get; set; }
        public Type Resource { get; set; }
        public string ContentType { get; set; }
        public string ContentTypeVersion { get; set; }
        public string [] ContentTypeEncodings { get; set; }

        public Uri GetRelativePath(Type resourceDecorated)
        {
            var routeDirectory = this.Namespace.HasBlackSpace() ?
                this.Namespace
                :
                Web.Configuration.Settings.GetString(
                        AppSettings.DefaultNamespace,
                    (ns) => ns,
                    (whyUnspecifiedOrInvalid) => "api");

            var route = this.Route.HasBlackSpace() ?
                this.Route
                :
                resourceDecorated.Name;

            return new Uri($"{routeDirectory}/{route}", UriKind.Relative);
        }

        protected static IDictionary<Type, HttpMethod> methodLookup =
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

        protected virtual IDictionary<HttpMethod, MethodInfo[]> PossibleHttpMethods(Type controllerType, IApplication httpApp)
        {
            var actionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(method => method.ContainsCustomAttribute<HttpActionAttribute>())
                .GroupBy(method => method.GetCustomAttribute<HttpActionAttribute>().Method)
                .Select(methodGrp => (new HttpMethod(methodGrp.Key)).PairWithValue(methodGrp.ToArray()));

             return methodLookup
                .Select(
                    methodKvp => methodKvp.Value.PairWithValue(
                        controllerType
                            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                            .Where(method => method.ContainsCustomAttribute(methodKvp.Key))
                            .ToArray()))
                .Concat(actionMethods)
                .ToDictionary();
        }

        public virtual async Task<HttpResponseMessage> CreateResponseAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            string routeName)
        {
            var path = request.RequestUri.Segments
                .Skip(1)
                .Select(segment => segment.Trim('/'.AsArray()))
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();
            var possibleHttpMethods = PossibleHttpMethods(controllerType, httpApp);
            if (path.Length > 2)
            {
                var actionMethod = path[2];
                var matchingActionKeys = possibleHttpMethods
                    .SelectKeys()
                    .Where(key => String.Compare(key.Method, actionMethod, true) == 0);

                if (matchingActionKeys.Any())
                {
                    var actionHttpMethod = matchingActionKeys.First();
                    var matchingActionMethods = possibleHttpMethods[actionHttpMethod];
                    return await CreateResponseAsync(httpApp,
                        request, cancellationToken,
                        routeName, matchingActionMethods);
                }
            }

            var matchingKey = possibleHttpMethods
                .SelectKeys()
                .Where(key => String.Compare(key.Method, request.Method.Method, true) == 0);

            if (!matchingKey.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            var httpMethod = matchingKey.First();
            var matchingMethods = possibleHttpMethods[httpMethod];

            return await CreateResponseAsync(httpApp,
                request, cancellationToken,
                routeName, matchingMethods);
        }

        #region Invoke correct method

        public delegate TResult ParseContentDelegate<TResult>(Type type, Func<object, TResult> onParsed, Func<string, TResult> onFailure);

        private static async Task<HttpResponseMessage> CreateResponseAsync(IApplication httpApp,
            HttpRequestMessage request, CancellationToken cancellationToken, string controllerName, MethodInfo[] methods)
        {
            #region setup query parameter casting

            var fileNameParams = request.RequestUri.AbsoluteUri
                .MatchRegexInvoke(
                        $".*/(?i){controllerName}(?-i)/(?<defaultQueryParam>[a-zA-Z0-9-]+)",
                        defaultQueryParam => defaultQueryParam,
                    (string[] urlFileNames) =>
                    {
                        return urlFileNames;
                    });

            var queryParameters = request.RequestUri.ParseQuery()
                .Select(kvp => kvp.Key.ToLower().PairWithValue(kvp.Value))
                .ToDictionary();

            var queryParameterCollections = GetCollectionParameters(httpApp, queryParameters).ToDictionary();
            CastDelegate queryCastDelegate =
                (paramInfo, onParsed, onFailure) =>
                {
                    var queryKey = paramInfo
                        .GetAttributeInterface<IBindApiValue>()
                        .GetKey(paramInfo)
                        .ToLower();
                    var type = paramInfo.ParameterType;
                    if (!queryParameters.ContainsKey(queryKey))
                    {
                        if (!queryParameterCollections.ContainsKey(queryKey))
                            return onFailure($"Missing query parameter `{queryKey}`");
                        return queryParameterCollections[queryKey](
                                type,
                                vs => onParsed(vs),
                                why => onFailure(why));
                    }
                    var queryValueString = queryParameters[queryKey];
                    var queryValue = new QueryParamTokenParser(queryValueString);
                    return httpApp.Bind(queryValueString, paramInfo,
                            v => onParsed(v),
                            (why) => onFailure(why));
                };

            #endregion

            #region Get file name from URI (optional part between the controller name and the query)

            CastDelegate fileNameCastDelegate =
                (paramInfo, onParsed, onFailure) =>
                {
                    if (!fileNameParams.Any())
                        return onFailure("No URI filename value provided.");
                    var type = paramInfo.ParameterType;
                    return httpApp.Bind(fileNameParams.First(), paramInfo,
                        v => onParsed(v),
                        (why) => onFailure(why));
                };

            #endregion

            return await httpApp.GetType()
                .GetAttributesInterface<IParseContent>(true, true)
                .Where(contentParser => contentParser.DoesParse(request))
                .First(
                    (contentParser, next) =>
                    {
                        return contentParser.ParseContentValuesAsync(httpApp, request,
                            (bodyCastDelegate, bodyValues) => GetResponseAsync(methods,
                                queryCastDelegate,
                                bodyCastDelegate,
                                fileNameCastDelegate,
                                httpApp, request, cancellationToken,
                                queryParameters.SelectKeys(),
                                bodyValues,
                                fileNameParams.Any()));
                    },
                    () =>
                    {
                        var mediaType = request.Content.Headers.IsDefaultOrNull() ?
                           string.Empty
                           :
                           request.Content.Headers.ContentType.IsDefaultOrNull() ?
                               string.Empty
                               :
                               request.Content.Headers.ContentType.MediaType;
                        CastDelegate parser =
                            (paramInfo, onParsed, onFailure) => onFailure(
                                $"Could not parse content of type {mediaType}");
                        return GetResponseAsync(methods,
                            queryCastDelegate,
                            parser,
                            fileNameCastDelegate,
                            httpApp, request, cancellationToken,
                            queryParameters.SelectKeys(),
                            new string[] { },
                            fileNameParams.Any());
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

        public static IEnumerable<KeyValuePair<string, ParseContentDelegate<SelectParameterResult>>> GetCollectionParameters(IApplication httpApp, IEnumerable<KeyValuePair<string, string>> queryParameters)
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
                                    httpApp.Bind(param.Value, type,
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
                                            var kvpCreateMethod = typeof(FunctionViewControllerAttribute).GetMethod("KvpCreate", BindingFlags.Static | BindingFlags.NonPublic);
                                            var correctGenericKvpCreate = kvpCreateMethod.MakeGenericMethod(genericArgs);
                                            var lookup = collectionParameterGrp
                                                .FlatMap(
                                                    (collectionParameter, next, skip) =>
                                                        (object[])collectionParameter.fetchValue(typeToCast,
                                                            v => next(correctGenericKvpCreate.Invoke(null, new object[] { collectionParameter.key, v })),
                                                            (why) => skip()),
                                                    (IEnumerable<object> lookupInner) => lookupInner.ToArray());

                                            var castMethod = typeof(FunctionViewControllerAttribute).GetMethod("CastToKvp", BindingFlags.Static | BindingFlags.NonPublic);
                                            var correctKvpsCast = castMethod.MakeGenericMethod(genericArgs);
                                            var kvpsOfCorrectTypes = correctKvpsCast.Invoke(null, lookup.AsArray());

                                            var dictCreateMethod = typeof(FunctionViewControllerAttribute).GetMethod("DictionaryCreate", BindingFlags.Static | BindingFlags.NonPublic);
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

        protected struct MethodCast
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
                                var validator = paramResult.parameterInfo.GetAttributeInterface<IBindApiValue>();
                                var lookupName = validator.GetKey(paramResult.parameterInfo);
                                var location = paramResult.Location;
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

                    if (contentFailedValidations.IsNullOrWhiteSpace())
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

        public static async Task<HttpResponseMessage> GetResponseAsync(MethodInfo[] methods,
            CastDelegate fetchQueryParam,
            CastDelegate fetchBodyParam,
            CastDelegate fetchNameParam,
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            IEnumerable<string> queryKeys, IEnumerable<string> bodyKeys, bool hasFileParam)
        {
            var methodsForConsideration = methods
                .Select(
                    (method) =>
                    {
                        var parametersCastResults = method
                            .GetParameters()
                            .Where(param => param.ContainsAttributeInterface<IBindApiValue>())
                            .Select(
                                (param) =>
                                {
                                    var castValue = param.GetAttributeInterface<IBindApiValue>();
                                    return castValue.TryCast(httpApp, request, method, param,
                                            fetchQueryParam,
                                            fetchBodyParam,
                                            fetchNameParam);
                                })
                            .ToArray();

                        var failedValidations = parametersCastResults
                            .Where(pcr => !pcr.valid)
                            .ToArray();
                        return HasExtraParameters(method,
                                queryKeys,
                                bodyKeys, hasFileParam,
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
                                    valid = true,
                                    method = method,
                                    parametersWithValues = parametersWithValues,
                                };
                            },
                            (extraParams) =>
                            {
                                return new MethodCast
                                {
                                    valid = false,
                                    failedValidations = failedValidations,
                                    method = method,
                                    extraBodyParams = extraParams,
                                };
                            });
                    });
            //var debugConsider = await methodsForConsideration.ToArrayAsync();
            var validMethods = methodsForConsideration
                .Where(methodCast => methodCast.valid);
            //var debug = await validMethods.ToArrayAsync();
            return await validMethods
                .First(
                    (methodCast, next) =>
                    {
                        return InvokeValidatedMethod(httpApp, request, cancellationToken,
                                methodCast.method, methodCast.parametersWithValues);
                    },
                    () =>
                    {
                        return Issues(methodsForConsideration).AsTask();
                    });

            HttpResponseMessage Issues(IEnumerable<MethodCast> methodsCasts)
            {
                var reasonStrings = methodsCasts
                    .Select(
                        methodCast =>
                        {
                            var errorMessage = methodCast.ErrorMessage;
                            return errorMessage;
                        })
                    .ToArray();
                if (!reasonStrings.Any())
                {
                    return request
                        .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                        .AddReason("No methods that implement Action");
                }
                var content = reasonStrings.Join(";");
                return request
                    .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                    .AddReason(content);
            }
        }

        private static TResult HasExtraParameters<TResult>(MethodInfo method,
                IEnumerable<string> queryKeys, IEnumerable<string> bodyKeys, bool hasFileParam,
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

                        if (hasFileParam)
                        {
                            if (verbAttr.MatchFileParameter)
                            {
                                var matchFileQueryLookup = matchedParameters
                                    .Where(param => param.fromFile);
                                if (!matchFileQueryLookup.Any())
                                    return onExtraParams(extraQueryKeys);
                            }
                        }
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

        private static Task<HttpResponseMessage> InvokeValidatedMethod(IApplication httpApp,
                HttpRequestMessage request, CancellationToken cancellationToken, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method
                .GetParameters()
                .SelectReduce(
                    async (methodParameter, next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(request, cancellationToken, methodParameter,
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

                            return (request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                                .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
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
                            if (ex is IHttpResponseMessageException)
                            {
                                var httpResponseMessageException = ex as IHttpResponseMessageException;
                                return httpResponseMessageException.CreateResponseAsync(
                                    httpApp, request, queryParameterOptions,
                                    method, methodParameters);
                            }
                            // TODO: Only do this in a development environment
                            return request.CreateResponse(HttpStatusCode.InternalServerError, ex.StackTrace).AddReason(ex.Message);
                        }

                    });
        }

        #endregion

        public virtual Route GetRoute(Type type, HttpApplication httpApp)
        {
            var methods = PossibleHttpMethods(type, httpApp).ToArray();
            return new Route(this.Route, methods, httpApp);
        }
    }
}
