using BlackBarLabs.Api.Resources;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using EastFive.Linq.Async;
using EastFive.Linq;
using EastFive.Api;

namespace BlackBarLabs.Api
{
    public interface IQueryParameter
    {
        TResult Parse<TResult>(
            Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable);
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class QueryParameterTypeAttribute : System.Attribute
    {
        public QueryParameterTypeAttribute()
        {
        }

        private Type webIdQueryType;
        public Type WebIdQueryType
        {
            get
            {
                return this.webIdQueryType;
            }
            set
            {
                webIdQueryType = value;
            }
        }

        private bool isOptional = false;
        public bool IsOptional
        {
            get
            {
                return isOptional;
            }
            set
            {
                isOptional = value;
            }
        }
    }

    public class QueryMatchAttribute : Attribute
    {
    }

    public static partial class QueryExtensions
    {
        private class QueryUnspecified : QueryMatchAttribute
        {
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>>              queriesSingle,
            IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>> queriesEnumerable,
            IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>            queriesArray)
        {
            return await GetQueryObjectParamters(query, request,
                async (queryNonNull, queryObjectParameters) =>
                {
                    var response = await queriesSingle.WhichFormat(queryObjectParameters,
                        async (selectedQueryFormat) =>
                        {
                            var responseCallback = selectedQueryFormat.Compile();
                            var responseSingle = await responseCallback(queryNonNull);
                            return responseSingle;
                        },
                        async () =>
                        {
                            var responsesMultipart = await queriesEnumerable.WhichFormat(queryObjectParameters,
                                async (selectedQueryFormat) =>
                                {
                                    var responsesEnumerable = await selectedQueryFormat.Compile()(queryNonNull);
                                    var responseMultipart = await request.CreateMultipartResponseAsync(responsesEnumerable);
                                    return responseMultipart;
                                },
                                async () =>
                                {
                                    var responseArray = await queriesArray.WhichFormat(queryObjectParameters,
                                        async (selectedQueryFormat) =>
                                        {
                                            var responsesArray = await selectedQueryFormat.Compile()(queryNonNull);
                                            var responseMultipart = await request.CreateMultipartResponseAsync(responsesArray);
                                            return responseMultipart;
                                        },
                                        () => request.CreateResponse(System.Net.HttpStatusCode.NotImplemented).ToTask());
                                    return responseArray;
                                });
                            return responsesMultipart;
                        });
                    return response;
                });
        }

        private static TResult WhichFormat<TQuery, TResult, TExpressionResult>(this IEnumerable<Expression<Func<TQuery, Task<TExpressionResult>>>> queryFormats,
            IDictionary<PropertyInfo, QueryMatchAttribute> queryObjectParameters,
            Func<Expression<Func<TQuery, Task<TExpressionResult>>>, TResult> found,
            Func<TResult> notFound)
        {
            var matchingFormat = queryFormats.Where(
                queryFormat =>
                {
                    var queryMethodParameters = GetQueryMethodParameters(queryFormat);
                    return IsMatch(queryObjectParameters, queryMethodParameters);
                });
            var result = matchingFormat.FirstOrDefault(
                (first) => found(first),
                () => notFound());
            return result;
        }

        private static bool IsMatch(IDictionary<PropertyInfo, QueryMatchAttribute> queryObjectParameters,
            IDictionary<PropertyInfo, QueryParameterTypeAttribute> queryMethodParameters)
        {
            var queryObjectParametersSpecified = queryObjectParameters
                // .Where(propKvp => propKvp.Value.IsSpecified())
                .ToArray();

            return queryObjectParameters.Merge(queryMethodParameters,
                queryObjectParameter => queryObjectParameter.Key,
                queryMethodParameter => queryMethodParameter.Key,
                (matched, unmatchedQueryParameters, unmatchedMethodOParameters) =>
                {
                    if (unmatchedQueryParameters.Any())
                        return false;

                    if (unmatchedMethodOParameters.SelectValues().Any(
                        qma => !qma.IsOptional))
                        return false;

                    // TODO: Activate and add the optionals

                    var matchedResult = matched.SelectValues().All(
                        match => match.Value.Value.WebIdQueryType.IsAssignableFrom(match.Key.Value.GetType()));
                    return matchedResult;
                },
                (propInfo1, propInfo2) =>
                {
                    return string.Compare(propInfo1.Name, propInfo2.Name) == 0;
                },
                (name) => name.Name.GetHashCode());
        }

        internal static async Task<HttpResponseMessage> GetQueryObjectParamters<TQuery>(TQuery query, HttpRequestMessage request,
            Func<TQuery, IDictionary<PropertyInfo, QueryMatchAttribute>, Task<HttpResponseMessage>> callback)
        {
            if (query.IsDefault())
            {
                var emptyQuery = Activator.CreateInstance<TQuery>();
                if (emptyQuery.IsDefault())
                    throw new Exception($"Could not activate object of type {typeof(TQuery).FullName}");
                return await GetQueryObjectParamters(emptyQuery, request, callback);
            }

            if (query is ResourceQueryBase)
            {
                var resourceQuery = query as ResourceQueryBase;
                if (resourceQuery.Id.IsDefault() &&
                   String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
                   request.RequestUri.Segments.Any())
                {
                    var idRefQuery = request.RequestUri.Segments.Last();
                    Guid idRefGuid;
                    if (Guid.TryParse(idRefQuery, out idRefGuid))
                        resourceQuery.Id = idRefGuid;
                }
            }

            if (query is ResourceBase)
            {
                var resource = query as ResourceBase;
                if (resource.Id.IsDefault() &&
                   String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
                   request.RequestUri.Segments.Any())
                {
                    var idRefQuery = request.RequestUri.Segments.Last();
                    Guid idRefGuid;
                    if (Guid.TryParse(idRefQuery, out idRefGuid))
                        resource.Id = idRefGuid;
                }
            }

            return await query.GetType().GetProperties()
                .SelectUntil<PropertyInfo, KeyValuePair<PropertyInfo, QueryMatchAttribute>?, Task<HttpResponseMessage>>(
                    (prop, cont, stop) =>
                    {
                        var value = prop.GetValue(query);
                        if (null == value)
                            return cont(default(KeyValuePair<PropertyInfo, QueryMatchAttribute>?));
                        if (typeof(IQueryParameter).IsInstanceOfType(value))
                        {
                            return ((IQueryParameter)value).Parse(
                                (v) => cont(new KeyValuePair<PropertyInfo, QueryMatchAttribute>(prop, v)),
                                (why) => stop(request.CreateResponse(System.Net.HttpStatusCode.BadRequest).AddReason(why).ToTask()));
                        }
                        return cont(default(KeyValuePair<PropertyInfo, QueryMatchAttribute>?));
                    },
                    (kvps) => callback(query, kvps.SelectWhereHasValue().ToDictionary()));
        }

        private static IDictionary<PropertyInfo, QueryParameterTypeAttribute> GetQueryMethodParameters<TQuery, TExpressionResult>(Expression<Func<TQuery, Task<TExpressionResult>>> queryFormat)
        {
            var args = queryFormat.GetArguments();
            return GetQueryMethodParamters(args);
        }

        private static ReadOnlyCollection<Expression> GetArguments(this LambdaExpression expression)
        {
            var bodyInvoca = expression.Body as InvocationExpression;
            if (default(InvocationExpression) != bodyInvoca)
                return bodyInvoca.Arguments;
            var bodyMethod = expression.Body as System.Linq.Expressions.MethodCallExpression;
            return bodyMethod.Arguments;
        }

        private static IDictionary<PropertyInfo, QueryParameterTypeAttribute> GetQueryMethodParamters(ReadOnlyCollection<Expression> arguments)
        {
            var kvps = arguments
                .Where(arg => arg is MethodCallExpression)
                .Select(arg => arg as MethodCallExpression)
                .Where(methodCall => methodCall.Arguments.Count > 0)
                .Where(methodCall => methodCall.Arguments.First() is MemberExpression)
                .Select(
                    methodCall =>
                    {
                        var args = methodCall.Arguments.First() as MemberExpression;
                        if (!(args.Member is PropertyInfo))
                            return default(KeyValuePair<PropertyInfo, QueryParameterTypeAttribute>?);
                        var memberExp = args.Member as PropertyInfo;
                        var method = methodCall.Method;
                        var customAttributes = method.GetCustomAttributes<QueryParameterTypeAttribute>().ToArray();
                        if (customAttributes.Length == 0)
                            return default(KeyValuePair<PropertyInfo, QueryParameterTypeAttribute>?);

                        var queryType = customAttributes.First();
                        return new KeyValuePair<PropertyInfo, QueryParameterTypeAttribute>(memberExp, queryType);
                    })
                .SelectWhereHasValue()
                .ToDictionary();
            return kvps;
        }
    }
}
