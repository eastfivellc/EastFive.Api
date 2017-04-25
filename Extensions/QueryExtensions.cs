using BlackBarLabs.Api.Resources;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

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
            where TQuery : ResourceQueryBase
        {
            return await GetQueryObjectParamters(query, request,
                async (queryNonNull, queryObjectParameters) =>
                {
                    var response = await queriesSingle.WhichFormatSingle(queryObjectParameters,
                        async (selectedQueryFormat) =>
                        {
                            var responseCallback = selectedQueryFormat.Compile();
                            var responseSingle = await responseCallback(queryNonNull);
                            return responseSingle;
                        }, 
                        async () =>
                        {
                            var responsesMultipart = await queriesEnumerable.WhichFormatEnumerable(queryObjectParameters,
                                async (selectedQueryFormat) =>
                                {
                                    var responsesEnumerable = await selectedQueryFormat.Compile()(queryNonNull);
                                    var responseMultipart = await request.CreateMultipartResponseAsync(responsesEnumerable);
                                    return responseMultipart;
                                },
                                async () =>
                                {
                                    var responseArray = await queriesArray.WhichFormatArray(queryObjectParameters,
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

        private static TResult WhichFormatSingle<TQuery, TResult>(this IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>> queryFormats,
            IDictionary<PropertyInfo, QueryMatchAttribute> queryObjectParameters,
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
            IDictionary<PropertyInfo, QueryMatchAttribute> queryObjectParameters,
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
            IDictionary<PropertyInfo, QueryMatchAttribute> queryObjectParameters,
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

        private static bool IsMatch(IDictionary<PropertyInfo, QueryMatchAttribute> queryObjectParameters,
            IDictionary<PropertyInfo, Type> queryMethodParameters)
        {
            var queryObjectParametersSpecified = queryObjectParameters
                // .Where(propKvp => propKvp.Value.IsSpecified())
                .ToArray();

            if (queryObjectParametersSpecified.Length != queryMethodParameters.Keys.Count)
                return false;

            return queryObjectParametersSpecified.All(
                queryObjectParameter =>
                {
                    bool foundMatch = queryMethodParameters
                        .Any(queryMethodParameter =>
                            string.Compare(queryMethodParameter.Key.Name, queryObjectParameter.Key.Name) == 0 &&
                            queryMethodParameter.Value.IsInstanceOfType(queryObjectParameter.Value));
                    return foundMatch;
                });
        }

        private static async Task<HttpResponseMessage> GetQueryObjectParamters<TQuery>(TQuery query, HttpRequestMessage request,
            Func<TQuery, IDictionary<PropertyInfo, QueryMatchAttribute>, Task<HttpResponseMessage>> callback)
            where TQuery : ResourceQueryBase
        {
            if (default(TQuery) == query)
            {
                var emptyQuery = Activator.CreateInstance<TQuery>();
                if (default(TQuery) == emptyQuery)
                    throw new Exception($"Could not activate object of type {typeof(TQuery).FullName}");
                return await GetQueryObjectParamters(emptyQuery, request, callback);
            }

            if(default(WebIdQuery) == query.Id &&
               String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
               request.RequestUri.Segments.Any())
            {
                var idRefQuery = request.RequestUri.Segments.Last();
                Guid idRefGuid;
                if (Guid.TryParse(idRefQuery, out idRefGuid))
                    query.Id = idRefGuid;
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

            //var queryPropertiesX = queryProperties
            //    .Where(
            //        (prop) =>
            //        {
            //            if (prop.PropertyType == typeof(WebIdQuery))
            //                return true;

            //            if (prop.PropertyType == typeof(DateTimeQuery))
            //                return true;

            //            if (prop.PropertyType == typeof(BoolQuery))
            //                return true;

            //            if (prop.PropertyType == typeof(BlackBarLabs.Api.ResourceQueryBase) &&
            //                prop.GetValue(query) != null)
            //                return true;

            //            prop.SetValue(replacementQuery, prop.GetValue(query));
            //            return false;
            //        })
            //    .Select(
            //        (queryProp) =>
            //        {
            //            if (queryProp.PropertyType == typeof(BlackBarLabs.Api.ResourceQueryBase))
            //            {
            //                var valueObj = (BlackBarLabs.Api.ResourceQueryBase)queryProp.GetValue(query);
            //                return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, new WebIdObject(valueObj));
            //            }

            //            if (queryProp.PropertyType == typeof(DateTimeQuery))
            //            {
            //                var valueDateTime = (DateTimeQuery)queryProp.GetValue(query);
            //                if (default(DateTimeQuery) == valueDateTime)
            //                    return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, new QueryUnspecified());

            //                var matchableReplacementValueDateTime = 
            //                        valueDateTime.Parse<IWebParsable>(
            //                            (from,to) => new DateTimeRangeQuery(from, to),
            //                            (when) => new DateTimeValue(when),
            //                            () => new DateTimeEmpty(),
            //                            () => new QueryUnspecified(),
            //                            () => new WebIdBadRequest(),
            //                            () => new QueryUnspecified());
            //                queryProp.SetValue(replacementQuery, matchableReplacementValueDateTime);
            //                return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, matchableReplacementValueDateTime);
            //            }

            //            if (queryProp.PropertyType == typeof(BoolQuery))
            //            {
            //                var valueBool = (BoolQuery)queryProp.GetValue(query);
            //                if (default(BoolQuery) == valueBool)
            //                    return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, new QueryUnspecified());

            //                var matchableReplacementValueBool =
            //                        valueBool.Parse<IWebParsable>(
            //                            (valueMaybe) => new BoolValue(valueMaybe.Value),
            //                            () => new QueryUnspecified(),
            //                            () => new BoolBadRequest());;
            //                queryProp.SetValue(replacementQuery, matchableReplacementValueBool);
            //                return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, matchableReplacementValueBool);
            //            }

            //            var value = (WebIdQuery)queryProp.GetValue(query);
            //            if (default(WebIdQuery) == value)
            //                return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, new QueryUnspecified());

            //            var over = value.Parse<IWebParsable>(request,
            //                        (guid) => new WebIdGuid(guid),
            //                        (guids) => new WebIdGuids(guids.ToArray()),
            //                        () => new QueryUnspecified(),
            //                        () => new WebIdEmpty(),
            //                        () => new WebIdBadRequest());
            //            queryProp.SetValue(replacementQuery, over);
            //            return new KeyValuePair<PropertyInfo, IWebParsable>(queryProp, over);
            //        });
            //var queryObjectParametersX =  queryPropertiesX.ToDictionary();
            //if (queryObjectParameters.Any(propKvp => propKvp.Value is WebIdBadRequest))
            //    return request.CreateResponse(System.Net.HttpStatusCode.BadRequest);

            //return await callback(queryObjectParameters, replacementQuery);
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
