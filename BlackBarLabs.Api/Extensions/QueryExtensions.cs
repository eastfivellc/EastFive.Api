using BlackBarLabs.Api.Resources;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Core.Collections;
using BlackBarLabs.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>>              queriesSingle,
            IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>> queriesEnumerable,
            IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>            queriesArray)
        {
            return await GetQueryObjectParamters(query, request,
                async (queryObjectParameters, replacementQuery) =>
                {
                    var response = await queriesSingle.WhichFormatSingle(queryObjectParameters,
                        async (selectedQueryFormat) =>
                        {
                            var responseSingle = await selectedQueryFormat.Compile()(replacementQuery);
                            return responseSingle;
                        },
                        async () =>
                        {
                            var responsesMultipart = await queriesEnumerable.WhichFormatEnumerable(queryObjectParameters,
                                async (selectedQueryFormat) =>
                                {
                                    var responsesEnumerable = await selectedQueryFormat.Compile()(replacementQuery);
                                    var responseMultipart = await request.CreateMultipartResponseAsync(responsesEnumerable);
                                    return responseMultipart;
                                },
                                async () =>
                                {
                                    var responseArray = await queriesArray.WhichFormatArray(queryObjectParameters,
                                        async (selectedQueryFormat) =>
                                        {
                                            var responsesArray = await selectedQueryFormat.Compile()(replacementQuery);
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

        private static TResult WhichFormatSingle<TQuery, TResult>(this IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>> queryFormats,
            IDictionary<PropertyInfo, WebIdQuery> queryObjectParameters,
            Func<Expression<Func<TQuery, Task<HttpResponseMessage>>>, TResult> found,
            Func<TResult> notFound)
        {
            var matchingFormat = queryFormats.Where(
                queryFormat =>
                {
                    var queryMethodParameters = GetQueryMethodParamters(queryFormat);
                    return IsMatch(queryObjectParameters, queryMethodParameters);
                });
            var result = matchingFormat.FirstOrDefault(
                (first) => found(first),
                () => notFound());
            return result;
        }
        
        private static TResult WhichFormatEnumerable<TQuery, TResult>(this IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>> queryFormats,
            IDictionary<PropertyInfo, WebIdQuery> queryObjectParameters,
            Func<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>, TResult> found,
            Func<TResult> notFound)
        {
            var matchingFormat = queryFormats.Where(
                queryFormat =>
                {
                    var queryMethodParameters = GetQueryMethodParamters(queryFormat);
                    return IsMatch(queryObjectParameters, queryMethodParameters);
                });
            var result = matchingFormat.FirstOrDefault(
                (first) => found(first),
                () => notFound());
            return result;
        }

        private static TResult WhichFormatArray<TQuery, TResult>(this IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>> queryFormats,
            IDictionary<PropertyInfo, WebIdQuery> queryObjectParameters,
            Func<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>, TResult> found,
            Func<TResult> notFound)
        {
            var matchingFormat = queryFormats.Where(
                queryFormat =>
                {
                    var queryMethodParameters = GetQueryMethodParamters(queryFormat);
                    return IsMatch(queryObjectParameters, queryMethodParameters);
                });
            var result = matchingFormat.FirstOrDefault(
                (first) => found(first),
                () => notFound());
            return result;
        }

        private static bool IsMatch(IDictionary<PropertyInfo, WebIdQuery> queryObjectParameters, IDictionary<PropertyInfo, Type> queryMethodParameters)
        {
            var queryObjectParametersMissing = queryObjectParameters
                .Where(propKvp => !(propKvp.Value is WebIdUnspecified))
                .Where(propKvp => !queryMethodParameters.ContainsKey(propKvp.Key) ||
                                  !queryMethodParameters[propKvp.Key].IsInstanceOfType(propKvp.Value));

            return !queryObjectParametersMissing.Any();
        }

        private static async Task<HttpResponseMessage> GetQueryObjectParamters<TQuery>(TQuery query, HttpRequestMessage request,
            Func<IDictionary<PropertyInfo, WebIdQuery>, TQuery, Task<HttpResponseMessage>> callback)
        {
            var replacementQuery = Activator.CreateInstance<TQuery>();

            var queryProperties = query.GetType().GetProperties()
                .Where(
                    (prop) =>
                    {
                        if (prop.PropertyType != typeof(WebIdQuery))
                        {
                            prop.SetValue(replacementQuery, prop.GetValue(query));
                            return false;
                        }
                        return true;
                    })
                .Select(
                    (queryProp) =>
                    {
                        var value = (WebIdQuery)queryProp.GetValue(query);
                        if (default(WebIdQuery) == value)
                            return new KeyValuePair<PropertyInfo, WebIdQuery>(queryProp, new WebIdUnspecified());
                        var over = value.Parse<WebIdQuery>(request,
                            (guid) => new WebIdGuid(guid),
                            (guids) => new WebIdGuids(guids.ToArray()),
                            () => new WebIdUnspecified(),
                            () => new WebIdEmpty(),
                            () => new WebIdBadRequest());
                        queryProp.SetValue(replacementQuery, over);

                        return new KeyValuePair<PropertyInfo, WebIdQuery>(queryProp, over);
                    });
            var queryObjectParameters =  queryProperties.ToDictionary();

            if (queryObjectParameters.Any(propKvp => propKvp.Value is WebIdBadRequest))
                return request.CreateResponse(System.Net.HttpStatusCode.BadRequest);

            return await callback(queryObjectParameters, replacementQuery);
        }

        private static IDictionary<PropertyInfo, Type> GetQueryMethodParamters<TQuery>(Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat)
        {
            var args = queryFormat.GetArguments();
            return GetQueryMethodParamters(args);
        }

        private static IDictionary<PropertyInfo, Type> GetQueryMethodParamters<TQuery>(Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat)
        {
            var args = queryFormat.GetArguments();
            return GetQueryMethodParamters(args);
        }

        private static IDictionary<PropertyInfo, Type> GetQueryMethodParamters<TQuery>(Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat)
        {
            var args = queryFormat.GetArguments();
            return GetQueryMethodParamters(args);
        }
        
        private static ReadOnlyCollection<Expression> GetArguments(this LambdaExpression expression)
        {
            var tbody = expression.Body.GetType();
            var bodyInvoca = expression.Body as InvocationExpression;
            if (default(InvocationExpression) != bodyInvoca)
                return bodyInvoca.Arguments;
            var bodyMethod = expression.Body as System.Linq.Expressions.MethodCallExpression;
            return bodyMethod.Arguments;
        }

        private static IDictionary<PropertyInfo, Type> GetQueryMethodParamters(ReadOnlyCollection<Expression> arguments)
        {
            var kvps = arguments
                .Where(arg => arg is MethodCallExpression)
                .Select(
                    (Expression arg) =>
                    {
                        var method = arg as MethodCallExpression;
                        var args = method.Arguments.First() as MemberExpression;
                        var memberExp = args.Member as PropertyInfo;
                        var queryType = method.Method.GetCustomAttribute<QueryParameterTypeAttribute>().WebIdQueryType;
                        return new KeyValuePair<PropertyInfo, Type>(memberExp, queryType);
                    })
                .ToDictionary();
            return kvps;
        }
        
    }
}
