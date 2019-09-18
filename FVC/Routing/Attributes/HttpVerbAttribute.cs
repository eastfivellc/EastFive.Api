﻿using BlackBarLabs.Extensions;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;



namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public abstract class HttpVerbAttribute : Attribute, IMatchRoute, IDocumentMethod
    {
        private bool matchAllParameters = true;
        public bool MatchAllParameters
        {
            get
            {
                return matchAllParameters;
            }
            set
            {
                matchAllParameters = value;
                matchAllBodyParameters = value;
                matchAllQueryParameters = value;
            }
        }

        private bool matchAllBodyParameters = false;
        public bool MatchAllBodyParameters
        {
            get
            {
                return matchAllBodyParameters;
            }
            set
            {
                if (!value)
                    matchAllParameters = false;
                matchAllBodyParameters = value;
            }
        }

        private bool matchAllQueryParameters = true;
        public bool MatchAllQueryParameters
        {
            get
            {
                return matchAllQueryParameters;
            }
            set
            {
                if (!value)
                    matchAllQueryParameters = false;
                matchAllQueryParameters = value;
            }
        }

        private bool matchFileParameter = true;
        public bool MatchFileParameter
        {
            get
            {
                return matchFileParameter;
            }
            set
            {
                if (!value)
                    matchFileParameter = false;
                matchFileParameter = value;
            }
        }

        public abstract string Method { get; }

        public virtual bool IsMethodMatch(MethodInfo method, HttpRequestMessage request, IApplication httpApp)
        {
            var isMethodMatch = String.Compare(this.Method, request.Method.Method, true) == 0;
            return isMethodMatch;
        }

        public async Task<RouteMatch> IsRouteMatch(
            MethodInfo method, HttpRequestMessage request, IApplication httpApp,
            IEnumerable<string> bodyKeys, CastDelegate<SelectParameterResult> fetchBodyParam)
        {
            var fileNameCastDelegate = GetFileNameCastDelegate(request, httpApp, out string [] pathKeys);
            var fetchQueryParam = GetQueryCastDelegate(request, httpApp, out string [] queryKeys);
            var parametersCastResults = await method
                .GetParameters()
                .Where(param => param.ContainsAttributeInterface<IBindApiValue>())
                .Select(
                    (param) =>
                    {
                        var castValue = param.GetAttributeInterface<IBindApiValue>();
                        return castValue.TryCastAsync(httpApp, request, method, param,
                            fetchQueryParam,
                            fetchBodyParam,
                            fileNameCastDelegate,
                            this.MatchFileParameter, 
                            this.MatchAllQueryParameters, 
                            this.MatchAllBodyParameters);
                    })
                .AsyncEnumerable()
                .ToArrayAsync();

            var failedValidations = parametersCastResults
                .Where(pcr => !pcr.valid)
                .ToArray();

            return HasExtraParameters(method,
                    pathKeys, queryKeys, bodyKeys,
                parametersCastResults,
                () =>
                {
                    if (failedValidations.Any())
                    {
                        return new RouteMatch
                        {
                            isValid = false,
                            failedValidations = failedValidations,
                            method = method,
                        };
                    }

                    var parametersWithValues = parametersCastResults
                        .Select(parametersCastResult =>
                            parametersCastResult.parameterInfo.PairWithValue(parametersCastResult.value))
                        .ToArray();

                    return new RouteMatch
                    {
                        isValid = true,
                        method = method,
                        parametersWithValues = parametersWithValues,
                    };
                },
                (extraFileParams, extraQueryParams, extraBodyParams) =>
                {
                    return new RouteMatch
                    {
                        isValid = false,
                        failedValidations = failedValidations,
                        method = method,
                        extraFileParams = extraFileParams,
                        extraQueryParams = extraQueryParams,
                        extraBodyParams = extraBodyParams,
                    };
                });
        }

        protected virtual CastDelegate<SelectParameterResult> GetFileNameCastDelegate(
            HttpRequestMessage request, IApplication httpApp, out string [] pathKeys)
        {
            var path = request.RequestUri.Segments
                .Skip(1)
                .Select(segment => segment.Trim('/'.AsArray()))
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();
            pathKeys = path.Skip(2).ToArray();
            CastDelegate<SelectParameterResult> fileNameCastDelegate =
                (query, type, onParsed, onFailure) =>
                {
                    if (path.Length < 3)
                        return onFailure("No URI filename value provided.").AsTask();
                    return httpApp
                        .Bind(type,
                                new QueryParamTokenParser(path[2]),
                            v => onParsed(v),
                            (why) => onFailure(why))
                        .AsTask();
                };
            return fileNameCastDelegate;
        }

        protected virtual CastDelegate<SelectParameterResult> GetQueryCastDelegate(
            HttpRequestMessage request, IApplication httpApp, out string[] queryKeys)
        {
            var queryParameters = request.RequestUri.ParseQuery()
                .Select(kvp => kvp.Key.ToLower().PairWithValue(kvp.Value))
                .ToDictionary();

            queryKeys = queryParameters.SelectKeys().ToArray();

            var queryParameterCollections = GetCollectionParameters(httpApp, queryParameters).ToDictionary();
            CastDelegate<SelectParameterResult> queryCastDelegate =
                (query, type, onParsed, onFailure) =>
                {
                    var queryKey = query.ToLower();
                    if (!queryParameters.ContainsKey(queryKey))
                    {
                        if (!queryParameterCollections.ContainsKey(queryKey))
                            return onFailure($"Missing query parameter `{queryKey}`").AsTask();
                        return queryParameterCollections[queryKey](
                                type,
                                vs => onParsed(vs),
                                why => onFailure(why))
                            .AsTask();
                    }
                    var queryValueString = queryParameters[queryKey];
                    var queryValue = new QueryParamTokenParser(queryValueString);
                    return httpApp
                        .Bind(type, queryValue,
                            v => onParsed(v),
                            (why) => onFailure(why))
                        .AsTask();
                };
            return queryCastDelegate;
        }

        private struct MultipartParameter
        {
            public string index;
            public string key;
            public Func<Type, Func<object, object>, Func<string, object>, object> fetchValue;
        }

        public delegate TResult ParseContentDelegate<TResult>(Type type, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

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
                                            .Select(
                                                (collectionParameter) =>
                                                    (SelectedValue ?)collectionParameter.fetchValue(typeToCast,
                                                        v => new SelectedValue { value = v },
                                                        (why) => default(SelectedValue?)))
                                            .SelectWhereHasValue()
                                            .Select(v => v.value);
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

        private struct SelectedValue
        {
            public object value;
        }

        protected virtual TResult HasExtraParameters<TResult>(MethodInfo method,
                IEnumerable<string> pathKeys, IEnumerable<string> queryKeys, IEnumerable<string> bodyKeys,
                IEnumerable<SelectParameterResult> matchedParameters,
            Func<TResult> noExtraParameters,
            Func<string[], string[], string[], TResult> onExtraParams)
        {
            if (!this.MatchAllParameters)
                return noExtraParameters();

            if (this.MatchFileParameter)
            {
                var matchParamFileCount = matchedParameters
                       .Where(param => param.fromFile)
                       .Count();
                var extraFileKeys = pathKeys.Count();
                if (extraFileKeys > matchParamFileCount)
                    return onExtraParams(pathKeys.ToArray(), new string[] { }, new string[] { });
            }

            if (this.MatchAllQueryParameters)
            {
                var matchParamQueryLookup = matchedParameters
                    .Where(param => param.fromQuery)
                    .ToLookup(param => param.key);
                var extraQueryKeys = queryKeys
                    .Where(queryKey => !matchParamQueryLookup.Contains(queryKey))
                    .ToArray();
                if (extraQueryKeys.Any())
                    return onExtraParams(new string[] { }, extraQueryKeys, new string[] { });
            }

            if (this.MatchAllBodyParameters)
            {
                var matchBodyLookup = matchedParameters
                    .Where(param => param.fromBody)
                    .ToLookup(param => param.key);
                var extraQueryKeys = bodyKeys
                    .Where(queryKey => !matchBodyLookup.Contains(queryKey))
                    .ToArray();
                if (extraQueryKeys.Any())
                    return onExtraParams(new string[] { }, new string[] { }, extraQueryKeys);
            }

            return noExtraParameters();
        }

        public Method GetMethod(MethodInfo methodInfo, HttpApplication httpApp)
        {
            return new Method(this.Method, methodInfo, httpApp);
        }
    }
}