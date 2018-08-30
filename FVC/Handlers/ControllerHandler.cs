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

        protected override async Task<HttpResponseMessage> SendAsync(HttpApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string filePath = request.RequestUri.AbsolutePath;
            var path = filePath.Split(new char[] { '/' }).Where(pathPart => !pathPart.IsNullOrWhiteSpace()).ToArray();
            var routeName =  (path.Length >= 2 ? path[1] : "").ToLower();

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
                () => base.SendAsync(request, cancellationToken));
        }

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
                    (type, onParsed, onFailure) => httpApp.StringContentToType(type, kvp.Value, v => onParsed(v), (why) => onFailure(why))))

                // File name from URI
                .If(true,
                    queryParamsFromUri => request.RequestUri.AbsoluteUri.MatchRegexInvoke($".*/(?i){controllerName}(?-i)/(?<defaultQueryParam>[a-zA-Z0-9-]+)",
                        defaultQueryParam => queryParamsFromUri
                                .Append(defaultKeyPlaceholder.PairWithValue<string, ParseContentDelegate<object[]>>(
                                    (type, onParsed, onFailure) => httpApp.StringContentToType(type,
                                        defaultQueryParam, v => onParsed(v),
                                        (why) => onFailure(why)))),
                        updates => updates.Any()? updates.First() : queryParamsFromUri))
                
                // Body parameters
                .Concat((await httpApp.ParseContentValuesAsync(request.Content))
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
                                                    var kvpCreateMethod = typeof(ControllerHandler).GetMethod("KvpCreate", BindingFlags.Static | BindingFlags.NonPublic);
                                                    var correctGenericKvpCreate = kvpCreateMethod.MakeGenericMethod(genericArgs);
                                                    var lookup = collectionParameterGrp
                                                        .FlatMap(
                                                            (collectionParameter, next, skip) =>
                                                                collectionParameter.fetchValue(typeToCast,
                                                                    v => next(correctGenericKvpCreate.Invoke(null, new object[] { collectionParameter.key, v } )),
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
                                            var lookupName = validator.Name.IsNullOrWhiteSpace() ?
                                                parameterRequiringValidation.Name.ToLower()
                                                :
                                                validator.Name.ToLower();

                                            // Handle default params
                                            if(!queryParams.ContainsKey(lookupName))
                                                lookupName = parameterRequiringValidation.GetCustomAttribute<QueryDefaultParameterAttribute, string>(
                                                    defaultAttr => defaultKeyPlaceholder,
                                                    () => lookupName);

                                            if (!queryParams.ContainsKey(lookupName))
                                                return await await validator.OnEmptyValueAsync(httpApp, request, parameterRequiringValidation,
                                                    v => validValue(parameterRequiringValidation.PairWithValue(v)),
                                                    () => didNotValidate(parameterRequiringValidation.PairWithValue("Value not provided")));

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
                            (addParamsNamed.Any()    ? $"Please correct the value for [{addParamsNamed.Select(uvs => uvs.Select(uv => $"{uv.Key} ({uv.Value})").Join(",")).Join(" or ")}]." : "")
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

                        if (httpApp.instigators.ContainsKey(methodParameter.ParameterType))
                            return await httpApp.instigators[methodParameter.ParameterType](httpApp, request, methodParameter, 
                                (v) => next(v));

                        if (methodParameter.ParameterType.IsInstanceOfType(httpApp))
                            return await next(httpApp);

                        return request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                            .AddReason($"Could not instigate type: {methodParameter.ParameterType.FullName}. Please add an instigator for that type.");
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

                        } catch(Exception ex)
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
