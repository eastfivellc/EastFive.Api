using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Api.Bindings;
using EastFive.Api.Core;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using System.IO;
using Newtonsoft.Json;
using EastFive.Api.Bindings.ContentHandlers;

namespace EastFive.Api
{
    public class FunctionViewControllerAttribute 
        : Attribute, IInvokeResource, IDocumentRoute, IProvideSerialization
    {
        public string Namespace { get; set; }
        public string ExcludeNamespaces { get; set; }
        public string Route { get; set; }
        public string Prefix { get; set; }
        [Obsolete]
        public Type Resource { get; set; }
        public string ContentType { get; set; }

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            return 0.0;
        }

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

        public async virtual Task<IHttpResponse> CreateResponseAsync(Type controllerType,
            IApplication httpApp, IHttpRequest request)
        {
            var matchingActionMethods = GetHttpMethods(controllerType,
                httpApp, request);
            if (!matchingActionMethods.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            return await httpApp.GetType()
                .GetAttributesInterface<IParseContent>(true, true)
                .Where(contentParser => contentParser.DoesParse(request))
                .First(
                    (contentParser, next) =>
                    {
                        return contentParser.ParseContentValuesAsync(httpApp, request,
                            (bodyCastDelegate, bodyValues) =>
                                InvokeMethod(
                                    matchingActionMethods,
                                    httpApp, request,
                                    bodyCastDelegate, bodyValues));
                    },
                    () =>
                    {
                        if (!request.HasBody)
                        {
                            CastDelegate parserEmpty =
                                (paramInfo, onParsed, onFailure) => onFailure(
                                    $"Request did not contain any content.");
                            return InvokeMethod(
                                matchingActionMethods,
                                httpApp, request,
                                parserEmpty, new string[] { });
                        }
                        var mediaType = request.GetMediaType();
                        CastDelegate parser =
                            (paramInfo, onParsed, onFailure) => onFailure(
                                $"Could not parse content of type {mediaType}");
                        return InvokeMethod(
                            matchingActionMethods,
                            httpApp, request,
                            parser, new string[] { });
                    });
        }

        #region Invoke correct method

        protected virtual IEnumerable<MethodInfo> GetHttpMethods(Type controllerType,
            IApplication httpApp, IHttpRequest request)
        {
            var matchingActionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(controllerType))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .Where(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsMethodMatch(method, request, httpApp);
                    });
            return matchingActionMethods;
        }

        protected virtual async Task<IHttpResponse> InvokeMethod(
            IEnumerable<MethodInfo> matchingActionMethods,
            IApplication httpApp, IHttpRequest routeData,
            CastDelegate bodyCastDelegate, string[] bodyValues)
        {
            var evaluatedMethods = matchingActionMethods
                .Select(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsRouteMatch(method, this, routeData, httpApp,
                            bodyValues, bodyCastDelegate);
                    });

            var validMethods = evaluatedMethods
                .Where(methodCast => methodCast.isValid);

            return await validMethods
                .First(
                    (methodCast, next) =>
                    {
                        return methodCast.parametersWithValues
                            .Aggregate<SelectParameterResult, ValidateHttpDelegate>(
                                (parameterSelectionUnvalidated, methodFinalUnvalidated, httpAppFinalUnvalidated, requestFinalUnvalidated) =>
                                {
                                    return methodFinalUnvalidated
                                        .GetAttributesInterface<IValidateHttpRequest>(true, true)
                                        .Aggregate<IValidateHttpRequest, ValidateHttpDelegate>(
                                            (parameterSelection, methodFinal, httpAppFinal, routeDataFinal) =>
                                            {
                                                return InvokeValidatedMethodAsync(
                                                    httpAppFinal, routeDataFinal,
                                                    methodFinal,
                                                    parameterSelection);
                                            },
                                            (callback, validator) =>
                                            {
                                                return (parametersSelected, methodCurrent, httpAppCurrent, requestCurrent) =>
                                                {
                                                    return validator.ValidateRequest(parametersSelected,
                                                        methodCurrent,
                                                        httpAppCurrent, requestCurrent,
                                                        callback);
                                                };
                                            })
                                        .Invoke(
                                            parameterSelectionUnvalidated,
                                            methodFinalUnvalidated, httpAppFinalUnvalidated, requestFinalUnvalidated);
                                },
                                (callback, parameterSelection) =>
                                {
                                    ValidateHttpDelegate boundCallback =
                                        (parametersSelected, methodCurrent, httpAppCurrent, requestCurrent) =>
                                        {
                                            var paramKvp = parameterSelection.parameterInfo
                                                .PairWithValue(parameterSelection.value);
                                            var updatedParameters = parametersSelected
                                                .Append(paramKvp)
                                                .ToArray();
                                            return callback(updatedParameters, methodCurrent, httpAppCurrent, requestCurrent);
                                        };
                                    var validators = parameterSelection.parameterInfo.ParameterType
                                        .GetAttributesInterface<IValidateHttpRequest>(true, true);
                                    if (!validators.Any())
                                        return boundCallback;
                                    var validator = validators.First();
                                    return (parametersSelected, methodCurrent, httpAppCurrent, requestCurrent) =>
                                    {
                                        return validator.ValidateRequest(parametersSelected,
                                            methodCurrent,
                                            httpAppCurrent, requestCurrent,
                                            boundCallback);
                                    };
                                })
                            .Invoke(
                                new KeyValuePair<ParameterInfo, object>[] { },
                                methodCast.method, httpApp, routeData);
                    },
                    () =>
                    {
                        return Issues(evaluatedMethods).AsTask();
                    });

            IHttpResponse Issues(IEnumerable<RouteMatch> methodsCasts)
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
                    return routeData
                        .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                        .AddReason("No methods that implement Action");
                }
                var content = reasonStrings.Join(";");
                return routeData
                    .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                    .AddReason(content);
            }
        }

        protected static Task<IHttpResponse> InvokeValidatedMethodAsync(
            IApplication httpApp, IHttpRequest routeData,
            MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            return httpApp.GetType()
                .GetAttributesInterface<IHandleMethods>(true, true)
                .Aggregate<IHandleMethods, MethodHandlingDelegate>(
                    (methodFinal, queryParametersFinal, httpAppFinal, routeDataFinal) =>
                    {
                        var response = InvokeHandledMethodAsync(httpApp, routeDataFinal, method, queryParameters);
                        return response;
                    },
                    (callback, methodHandler) =>
                    {
                        return (methodCurrent, queryParametersCurrent, httpAppCurrent, requestCurrent) =>
                            methodHandler.HandleMethodAsync(methodCurrent,
                                queryParametersCurrent, httpAppCurrent, requestCurrent,
                                callback);
                    })
                .Invoke(method, queryParameters, httpApp, routeData);
        }

        protected static Task<IHttpResponse> InvokeHandledMethodAsync(
            IApplication httpApp, IHttpRequest routeData,
            MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (ParameterInfo methodParameter, Func<object, Task<IHttpResponse>> next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(routeData, methodParameter, next);
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
                            if (method.IsGenericMethod)
                            {
                                var genericArguments = method.GetGenericArguments().Select(arg => arg.Name).Join(",");
                                return routeData.CreateResponse(HttpStatusCode.InternalServerError)
                                    .AddReason($"Could not invoke {method.DeclaringType.FullName}..{method.Name} because it contains generic arguments:{genericArguments}");
                            }

                            var response = method.Invoke(null, methodParameters);
                            if (typeof(Api.IHttpResponse).IsAssignableFrom(method.ReturnType))
                                return ((Api.IHttpResponse)response);
                            if (typeof(Task<Api.IHttpResponse>).IsAssignableFrom(method.ReturnType))
                                return (await (Task<Api.IHttpResponse>)response);
                            if (typeof(Task<Task<Api.IHttpResponse>>).IsAssignableFrom(method.ReturnType))
                                return (await await (Task<Task<Api.IHttpResponse>>)response);

                            return (routeData.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                                .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
                        }
                        catch (TargetInvocationException ex)
                        {
                            var paramList = methodParameters.Select(p => p.GetType().FullName).Join(",");
                            var body = ex.InnerException.IsDefaultOrNull() ?
                                ex.StackTrace
                                :
                                $"[{ex.InnerException.GetType().FullName}]{ex.InnerException.Message}:\n{ex.InnerException.StackTrace}";
                            return routeData
                                .CreateResponse(HttpStatusCode.InternalServerError, body)
                                .AddReason($"Could not invoke {method.DeclaringType.FullName}.{method.Name}({paramList})");
                        }
                        catch (Exception ex)
                        {
                            return await httpApp.GetType()
                                .GetAttributesInterface<IHandleExceptions>(true, true)
                                .Aggregate(
                                    (Exception exFinal, MethodInfo methodFinal, KeyValuePair<ParameterInfo, object>[] queryParametersFinal, IApplication httpAppFinal, IHttpRequest routeDataFinal) =>
                                    {
                                        if (ex is IHttpResponseMessageException)
                                        {
                                            var httpResponseMessageException = ex as IHttpResponseMessageException;
                                            return httpResponseMessageException.CreateResponseAsync(
                                                httpApp, routeDataFinal, queryParameterOptions,
                                                method, methodParameters).AsTask<Api.IHttpResponse>();
                                        }

                                        return routeData
                                            .CreateResponse(HttpStatusCode.InternalServerError)
                                            .AddReason(ex.Message)
                                            .AsTask<Api.IHttpResponse>();
                                    },
                                    (HandleExceptionDelegate callback, IHandleExceptions methodHandler) =>
                                    {
                                        return (Exception exCurrent, MethodInfo methodCurrent, KeyValuePair<ParameterInfo, object>[] queryParametersCurrent, IApplication httpAppCurrent, IHttpRequest requestCurrent) =>
                                            methodHandler.HandleExceptionAsync(exCurrent, methodCurrent,
                                            queryParametersCurrent, httpAppCurrent, requestCurrent,
                                            callback);
                                    })
                                .Invoke(ex, method, queryParameters, httpApp, routeData);
                        }

                    });
        }

        #endregion

        public virtual Route GetRoute(Type type, HttpApplication httpApp)
        {
            var actionMethods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(type))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .ToArray();

            var ns = this.Namespace.HasBlackSpace() ? this.Namespace : "api";
            return new Route(type, ns, this.Route,
                actionMethods,
                type.GetMembers(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance),
                httpApp);
        }

        public virtual bool DoesHandleRequest(Type type, IHttpRequest request, out double matchQuality)
        {
            matchQuality = 
                (this.Route.HasBlackSpace() ? 0 : 2) +
                (this.Namespace.HasBlackSpace() ? 0 : 1);

            if (IsExcluded())
                return false;

            if (!IsNamespaceCorrect())
                return false;

            if (this.Route.HasBlackSpace())
            {
                var doesMatch = DoesMatch(1, this.Route);
                return doesMatch;
            }

            return true;

            bool DoesMatch(int index, string value)
            {
                var requestUrl = request.GetAbsoluteUri();
                var path = requestUrl.AbsolutePath;
                var pathParameters = path
                    .Split('/'.AsArray())
                    .Where(v => v.HasBlackSpace())
                    .ToArray();
                if (pathParameters.Length <= index)
                    return false;
                var component = pathParameters[index];
                if (!component.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }

            bool IsNamespaceCorrect()
            {
                if (this.Namespace.IsNullOrWhiteSpace())
                    return true;

                if (!Namespace.Contains(','))
                    return DoesMatch(0, this.Namespace);
                
                var doesAnyNamespaceMatch = Namespace
                    .Split(',')
                    .Any(ns => DoesMatch(0, ns));
                return doesAnyNamespaceMatch;
            }

            bool IsExcluded()
            {
                if (this.ExcludeNamespaces.IsNullOrWhiteSpace())
                    return false;
                return this.ExcludeNamespaces
                    .Split(',')
                    .Select(exNs => exNs.ToLower())
                    .First(
                        (exNs, next) =>
                        {
                            if (DoesMatch(0, exNs))
                                return true;
                            return next();
                        },
                        () => false);
            }
        }

        #region Serialization

        public string MediaType => "application/json";

        public Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert(request, httpApp);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            var contentType = this.ContentType.HasBlackSpace() ?
                this.ContentType
                :
                this.MediaType;
            return responseStream.WriteResponseText(jsonObj, request);
            //using (var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
            //    new StreamWriter(responseStream, writerEncoding)
            //    :
            //    new StreamWriter(responseStream, Encoding.UTF8))
            //{
            //    await streamWriter.WriteAsync(jsonObj);
            //    await streamWriter.FlushAsync();
            //}
        }

        #endregion

        #region Collection parameters

        public delegate TResult ParseContentDelegate<TResult>(Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

        private struct MultipartParameter
        {
            public string index;
            public string key;
            public Func<Type, Func<object, object>, Func<string, object>, object> fetchValue;
        }

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

    }
}
