using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

using EastFive;
using EastFive.Collections.Generic;
using EastFive.Linq.Expressions;
using BlackBarLabs.Api.Resources;
using EastFive.Extensions;

namespace EastFive.Api
{
    public static class UriExtensions
    {
        public static WebIdQuery ParseQueryParameter<QueryType>(this Uri uri,
            Expression<Func<QueryType, WebIdQuery>> parameterExpr)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (webId) => webId,
                () => default(WebIdQuery));
        }

        public static TResult ParseQueryParameter<QueryType, TResult>(this Uri uri,
            Expression<Func<QueryType, WebIdQuery>> parameterExpr,
            Func<WebIdQuery, TResult> onFound,
            Func<TResult> onNotInQueryString)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (valueString) => (WebIdQuery)valueString,
                onFound,
                onNotInQueryString);
        }

        public static BoolQuery ParseQueryParameter<QueryType>(this Uri uri,
            Expression<Func<QueryType, BoolQuery>> parameterExpr)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (webId) => webId,
                () => default(BoolQuery));
        }

        public static TResult ParseQueryParameter<QueryType, TResult>(this Uri uri,
            Expression<Func<QueryType, BoolQuery>> parameterExpr,
            Func<BoolQuery, TResult> onFound,
            Func<TResult> onNotInQueryString)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (valueString) => (BoolQuery)valueString,
                onFound,
                onNotInQueryString);
        }

        public static IntQuery ParseQueryParameter<QueryType>(this Uri uri,
            Expression<Func<QueryType, IntQuery>> parameterExpr)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (webId) => webId,
                () => default(IntQuery));
        }

        public static TResult ParseQueryParameter<QueryType, TResult>(this Uri uri,
            Expression<Func<QueryType, IntQuery>> parameterExpr,
            Func<IntQuery, TResult> onFound,
            Func<TResult> onNotInQueryString)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (valueString) => (IntQuery)valueString,
                onFound,
                onNotInQueryString);
        }

        public static StringQuery ParseQueryParameter<QueryType>(this Uri uri,
            Expression<Func<QueryType, StringQuery>> parameterExpr)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (webId) => webId,
                () => default(StringQuery));
        }

        public static TResult ParseQueryParameter<QueryType, TResult>(this Uri uri,
            Expression<Func<QueryType, StringQuery>> parameterExpr,
            Func<StringQuery, TResult> onFound,
            Func<TResult> onNotInQueryString)
        {
            return uri.ParseQueryParameter(parameterExpr,
                (valueString) => (StringQuery)valueString,
                onFound,
                onNotInQueryString);
        }

        public static Uri Location<TResource>(this IQueryable<TResource> urlQuery,
            string routeName = "DefaultApi",
            System.Web.Http.Routing.UrlHelper urlHelper = default)
        {
            var expression = urlQuery.Expression;
            var provider = urlQuery.Provider;
            provider.Execute<TResource>(expression);
            System.Web.Http.Routing.UrlHelper GetUrlHelper()
            {
                if (!urlHelper.IsDefaultOrNull())
                    return urlHelper;
                if (urlQuery is RequestMessage<TResource>)
                    return new System.Web.Http.Routing.UrlHelper((urlQuery as RequestMessage<TResource>).Request);
                throw new ArgumentException("Could not determine value for urlHelper");
            }
            var validUrlHelper = GetUrlHelper();
            var baseUrl = validUrlHelper.GetLocation(expression.Type.GenericTypeArguments.First(), routeName);

            var queryParams = new Dictionary<string, string>();
                //expression
                //.Select(param => 
                //    param.GetUrlAssignment(
                //        (queryParamName, value) =>
                //        {
                //            return queryParamName
                //                .PairWithValue((string)application.CastResourceProperty(value, typeof(String)));
                //        }))
                //.ToDictionary();

            var queryUrl = baseUrl.SetQuery(queryParams);
            return queryUrl;
        }

    }
}
