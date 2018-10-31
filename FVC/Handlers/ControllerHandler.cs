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

        private const string defaultKeyPlaceholder = "__DEFAULT_ID__";

        delegate TResult ParseContentDelegate<TResult>(Type type, Func<object, TResult> onParsed, Func<string, TResult> onFailure);

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
                    var queryValue = queryParameters[queryKey];
                    return httpApp
                        .StringContentToType(type, queryValue,
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
                                if (urlFileNames.Any())
                                    return onFailure("No URI filename value provided.");
                                return httpApp.StringContentToType(type,
                                        urlFileNames.First(),
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
                                value => onParsed(value),
                                (why) => onFailure(why));
                        };
                    return await GetResponseAsync(methods,
                        queryCastDelegate,
                        bodyCastDelegate,
                        fileNameCastDelegate,
                        httpApp, request,
                        (method, paramResults,
                            onNoExtraParameters,
                            onExtraParameters) => HasExtraParameters(method,
                                queryParameters.SelectKeys(),
                                bodyValues,
                                paramResults,
                                onNoExtraParameters,
                                onExtraParameters));
                });
            
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
                                        .StringContentToType(type, param.Value,
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

        private struct SelectParameterResult
        {
            public bool valid;
            public object value;
            public string failure;
            public ParameterInfo parameterInfo;
            internal bool fromQuery;
            internal bool fromBody;
            internal string key;
        }

        private static async Task<HttpResponseMessage> GetResponseAsync(MethodInfo[] methods,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchNameParam,
            HttpApplication httpApp, HttpRequestMessage request,
            Func<
                MethodInfo, SelectParameterResult[],
                Func<Task<HttpResponseMessage>>,
                Func<string [], Task<HttpResponseMessage>>,
                Task<HttpResponseMessage>> hasExtraParameters)
        {
            var response = await methods
                .SelectPartition(
                    async (method, removeParams, addParams) =>
                    {
                        var parametersCastResults = await method
                            .GetParameters()
                            .Where(param => param.ContainsCustomAttribute<QueryValidationAttribute>())
                            .Select(
                                (param) =>
                                {
                                    var castValue = param.GetCustomAttribute<QueryValidationAttribute, IProvideApiValue>(
                                        (queryValidationAttribute) => queryValidationAttribute,
                                        () => throw new Exception("WHere check failed"));
                                    return castValue.TryCastAsync<SelectParameterResult>(httpApp, request, method, param,
                                            fetchQueryParam,
                                            fetchBodyParam,
                                            fetchNameParam,
                                        (value) =>
                                        {
                                            return new SelectParameterResult
                                            {
                                                valid = true,
                                                value = value,
                                                parameterInfo = param,
                                            };
                                        },
                                        (why) =>
                                        {
                                            return new SelectParameterResult
                                            {
                                                valid = false,
                                                failure = why,
                                                parameterInfo = param,
                                            };
                                        });
                                })
                            .WhenAllAsync();

                        var parametersRequiringValidationThatDidNotValidate = parametersCastResults.Where(pcr => !pcr.valid);
                        if (parametersRequiringValidationThatDidNotValidate.Any())
                            return await addParams(
                                parametersRequiringValidationThatDidNotValidate
                                    .Select(prv => prv.parameterInfo.PairWithValue(prv.failure))
                                    .ToArray());

                        return await hasExtraParameters(method, parametersCastResults,
                            () =>
                            {
                                var parametersWithValues = parametersCastResults
                                    .Select(parametersCastResult =>
                                        parametersCastResult.parameterInfo.PairWithValue(parametersCastResult.value))
                                    .ToArray();
                                return InvokeValidatedMethod(httpApp, request, method, parametersWithValues,
                                    (missingParams) => addParams(missingParams.Select(param => param.PairWithValue("Missing")).ToArray()));
                            },
                            (extraParams) => removeParams(extraParams));
                    },
                    (string[][] removeParams, KeyValuePair<ParameterInfo, string>[][] addParams) =>
                    {
                        var addParamsNamed = addParams
                            .Select(
                                addParamKvps =>
                                {
                                    return addParamKvps
                                        .Select(
                                            addParamKvp =>
                                            {
                                                var validator = addParamKvp.Key.GetCustomAttribute<QueryValidationAttribute>();
                                                var lookupName = validator.Name.IsNullOrWhiteSpace() ?
                                                    addParamKvp.Key.Name.ToLower()
                                                    :
                                                    validator.Name.ToLower();
                                                return lookupName.PairWithValue(addParamKvp.Value);
                                            })
                                        .ToArray();
                                })
                            .ToArray();

                        var content =
                            (addParamsNamed.Any() ? $"Please correct the value for [{addParamsNamed.Select(uvs => uvs.Select(uv => $"{uv.Key} ({uv.Value})").Join(",")).Join(" or ")}]." : "")
                            +
                            (removeParams.Any() ? $"Remove query parameters [{  removeParams.Select(uvs => uvs.Select(uv => uv).Join(",")).Join(" or ")}]." : "");
                        return request
                            .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                            .AddReason(content)
                            .ToTask();
                    });
            return response;
        }

        //private static async Task<HttpResponseMessage> GetResponseAsync2(MethodInfo [] methods,
        //    IDictionary<string, ParseContentDelegate<object[]>> queryParams,
        //    HttpApplication httpApp, HttpRequestMessage request)
        //{
        //    var response = await methods
        //        .SelectPartition(
        //            (method, removeParams, addParams) =>
        //            {
        //                return method
        //                    .GetParameters()
        //                    .SelectPartitionOptimized(
        //                        (param, validated, unvalidated) =>
        //                            param.ContainsCustomAttribute<QueryValidationAttribute>() ?
        //                                validated(param)
        //                                :
        //                                unvalidated(param),
        //                        (ParameterInfo[] parametersRequiringValidation, ParameterInfo[] parametersNotRequiringValidation) =>
        //                        {
        //                            return parametersRequiringValidation.SelectPartition(
        //                                async (parameterRequiringValidation, validValue, didNotValidate) =>
        //                                {
        //                                    var validator = parameterRequiringValidation.GetCustomAttribute<QueryValidationAttribute>();
        //                                    var lookupName = validator.Name.IsNullOrWhiteSpace() ?
        //                                        parameterRequiringValidation.Name
        //                                        :
        //                                        validator.Name;

        //                                    // Handle capitalization changes
        //                                    if (!queryParams.ContainsKey(lookupName))
        //                                        lookupName = lookupName.ToLower();

        //                                    // Handle default params
        //                                    if (!queryParams.ContainsKey(lookupName))
        //                                        lookupName = parameterRequiringValidation.GetCustomAttribute<QueryDefaultParameterAttribute, string>(
        //                                            defaultAttr => defaultKeyPlaceholder,
        //                                            () => lookupName);

        //                                    if (!queryParams.ContainsKey(lookupName))
        //                                        return await await validator.OnEmptyValueAsync(httpApp, request, parameterRequiringValidation,
        //                                            v => validValue(parameterRequiringValidation.PairWithValue(v)),
        //                                            () => didNotValidate(parameterRequiringValidation.PairWithValue("Value not provided")));

        //                                    QueryValidationAttribute.CastDelegate fetchParam =
        //                                        async (query, type, success, failure) =>
        //                                        {
        //                                            // Hack here
        //                                            var strArray = queryParams[lookupName](type,
        //                                                (value) => value.AsArray(),
        //                                                (why) => new object[] { "", why });
        //                                            if (strArray.Length == 1)
        //                                                return success(strArray[0]);
        //                                            return failure((string)strArray[1]);
        //                                        };

        //                                    return await await validator.TryCastAsync(httpApp, request, method, parameterRequiringValidation,
        //                                            fetchParam,
        //                                            fetchParam,
        //                                            fetchParam,
        //                                        v => validValue(parameterRequiringValidation.PairWithValue(v)),
        //                                        (why) => didNotValidate(parameterRequiringValidation.PairWithValue(why)));
        //                                },
        //                                async (KeyValuePair<ParameterInfo, object>[] parametersRequiringValidationWithValues, KeyValuePair<ParameterInfo, string>[] parametersRequiringValidationThatDidNotValidate) =>
        //                                {
        //                                    if (parametersRequiringValidationThatDidNotValidate.Any())
        //                                        return await addParams(parametersRequiringValidationThatDidNotValidate);

        //                                    var parametersNotRequiringValidationWithValues = parametersNotRequiringValidation
        //                                        .Where(unvalidatedParam => queryParams.ContainsKey(unvalidatedParam.Name.ToLower()))
        //                                        .Select(
        //                                            (unvalidatedParam) =>
        //                                            {
        //                                                var queryParamValue = queryParams[unvalidatedParam.Name.ToLower()](unvalidatedParam.ParameterType,
        //                                                    v => v.AsArray(),
        //                                                    why => unvalidatedParam.ParameterType.GetDefault().AsArray()).First();
        //                                                return unvalidatedParam.PairWithValue(queryParamValue);
        //                                            });

        //                                    var parametersWithValues = parametersNotRequiringValidationWithValues
        //                                        .Concat(parametersRequiringValidationWithValues)
        //                                        .ToArray();

        //                                    return await HasExtraParameters(method,
        //                                            parametersRequiringValidation.Concat(parametersNotRequiringValidation),
        //                                            queryParams.SelectKeys(),
        //                                        () => InvokeValidatedMethod(httpApp, request, method, parametersWithValues,
        //                                            (missingParams) => addParams(missingParams.Select(param => param.PairWithValue("Missing")).ToArray())),
        //                                        (extraParams) => removeParams(extraParams));

        //                                });
        //                        });
        //            },
        //            (string[][] removeParams, KeyValuePair<ParameterInfo, string>[][] addParams) =>
        //            {
        //                var addParamsNamed = addParams
        //                    .Select(
        //                        addParamKvps =>
        //                        {
        //                            return addParamKvps
        //                                .Select(
        //                                    addParamKvp =>
        //                                    {
        //                                        var validator = addParamKvp.Key.GetCustomAttribute<QueryValidationAttribute>();
        //                                        var lookupName = validator.Name.IsNullOrWhiteSpace() ?
        //                                            addParamKvp.Key.Name.ToLower()
        //                                            :
        //                                            validator.Name.ToLower();
        //                                        return lookupName.PairWithValue(addParamKvp.Value);
        //                                    })
        //                                .ToArray();
        //                        })
        //                    .ToArray();

        //                var content =
        //                    (addParamsNamed.Any() ? $"Please correct the value for [{addParamsNamed.Select(uvs => uvs.Select(uv => $"{uv.Key} ({uv.Value})").Join(",")).Join(" or ")}]." : "")
        //                    +
        //                    (removeParams.Any() ? $"Remove query parameters [{  removeParams.Select(uvs => uvs.Select(uv => uv).Join(",")).Join(" or ")}]." : "");
        //                return request
        //                    .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
        //                    .AddReason(content)
        //                    .ToTask();
        //            });
        //    return response;
        //}

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
                            .Where(queryKey => matchParamQueryLookup.Contains(queryKey))
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
                            .Where(queryKey => matchBodyLookup.Contains(queryKey))
                            .ToArray();
                        if (extraQueryKeys.Any())
                            return onExtraParams(extraQueryKeys);
                    }
                    
                    return noExtraParameters();
                },
                () => throw new ArgumentException("method", $"Method {method.DeclaringType.FullName}..{method.Name}(...) does not have an HttpVerbAttribute."));
        }

        private static TResult HasExtraParameters<TResult>(MethodInfo method, 
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

        private static Task<HttpResponseMessage> InvokeValidatedMethod(HttpApplication httpApp, HttpRequestMessage request, MethodInfo method, 
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

                        return await httpApp.Instigate(request, methodParameter,
                            v => next(v));
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
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
