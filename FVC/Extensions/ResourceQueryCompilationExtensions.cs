using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public static class ResourceQueryCompilationExtensions
    {
        public static TAggr Compile<TResource, TAggr, TMemberAttribute, TMethodAttribute>(this IQueryable<TResource> urlQuery,
                TAggr startingValue,
            Func<TAggr, TMethodAttribute, MethodInfo, Expression[], TAggr> onAttribute,
            params KeyValuePair<string, TMethodAttribute> [] baseClassAttrs)
        {
            IEnumerable<KeyValuePair<MethodInfo, Expression[]>> FlattenArgumentExpression(Expression argExpression)
            {
                if (argExpression is MethodCallExpression)
                {
                    var methodCallExpression = argExpression as MethodCallExpression;
                    var method = methodCallExpression.Method;
                    var isExtensionMethod = method.IsDefaultOrNull() ?
                        false
                        :
                        method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);
                    if (isExtensionMethod)
                    { 
                        foreach (var subExpr in FlattenArgumentExpression(methodCallExpression.Arguments.First()))
                            yield return subExpr;
                    }
                    var nonExtensionThisArgs = methodCallExpression.Arguments
                        .If(isExtensionMethod, args => args.Skip(1))
                        .Where(arg => !(arg is MethodCallExpression))
                        .ToArray();
                    yield return methodCallExpression.Method
                        .PairWithValue(nonExtensionThisArgs);
                }
                yield break;
            }

            var expression = urlQuery.Expression;
            var provider = urlQuery.Provider;
            return FlattenArgumentExpression(expression)
                .Aggregate(startingValue,
                    (aggr, methodArgsKvp) =>
                    {
                        var method = methodArgsKvp.Key;
                        var argumentExpressions = methodArgsKvp.Value;
                        var methodAttr = GetMethodAttr();
                        return onAttribute(aggr, methodAttr, method, argumentExpressions);

                        TMethodAttribute  GetMethodAttr()
                        {
                            var methodAttrs = method.GetAttributesInterface<TMethodAttribute>();
                            if (methodAttrs.Any())
                                return methodAttrs.First();

                            if (method.DeclaringType == typeof(System.Linq.Queryable))
                            {
                                var baseClassAttrsMatching = baseClassAttrs
                                    .Where(kvp => kvp.Key == method.Name);
                                if (baseClassAttrsMatching.Any())
                                {
                                    var baseClassAttr = baseClassAttrsMatching.First().Value;
                                    return baseClassAttr;
                                }
                            }
                            return default;
                        }
                    });
        }

        public static Uri Location<TResource>(this IQueryable<TResource> urlQuery)
        {
            string GetServerUrl()
            {
                if (urlQuery is RequestMessage<TResource>)
                {
                    var requestMessage = urlQuery as RequestMessage<TResource>;
                    return requestMessage.InvokeApplication.ServerLocation.AbsoluteUri.TrimEnd('/'.AsArray());
                }
                throw new ArgumentException("Could not determine value for server location.");
            }

            string GetRoutePrefix()
            {
                var routePrefixes = typeof(TResource)
                    .GetCustomAttributes<System.Web.Http.RoutePrefixAttribute>()
                    .Select(routePrefix => routePrefix.Prefix)
                    .ToArray();
                if (routePrefixes.Any())
                    return routePrefixes.First();

                if (urlQuery is RequestMessage<TResource>)
                {
                    var requestMessage = urlQuery as RequestMessage<TResource>;
                    return requestMessage.InvokeApplication.ApiRouteName;
                }
                throw new ArgumentException("Could not determine value for route prefix.");
            }

            string GetControllerName()
            {
                return typeof(TResource).GetCustomAttribute<FunctionViewControllerAttribute, string>(
                    (attr) => attr.Route,
                    () => typeof(TResource).Name
                        .TrimEnd("Controller",
                            (trimmedName) => trimmedName,
                            (originalName) => originalName)
                        .ToLower());
            }

            var serverUrl = GetServerUrl();
            var prefix = GetRoutePrefix().Trim('/'.AsArray());
            var controllerName = GetControllerName().TrimStart('/'.AsArray());
            Uri.TryCreate($"{serverUrl}/{prefix}/{controllerName}", UriKind.Absolute, out Uri baseUrl);

            var queryUrl = urlQuery.Compile<TResource, Uri, IFilterApiValues, IFilterApiValues>(
                    baseUrl,
                (url, attr, method, operand) =>
                {
                    var updatedUrl = attr.BindUrlQueryValue(url, method, operand);
                    return updatedUrl;
                },
                "Where".PairWithValue((IFilterApiValues)new ResourceQueryExtensions.BinaryComparisonQueryAttribute()));

            return queryUrl;
        }

        public static HttpRequestMessage Request<TResource>(this IQueryable<TResource> urlQuery,
            HttpMethod httpMethod = default,
            IInvokeApplication applicationInvoker = default,
            string routeName = "DefaultApi",
            System.Web.Http.Routing.UrlHelper urlHelper = default)
        {
            if (httpMethod.IsDefaultOrNull())
                httpMethod = HttpMethod.Get;
            HttpRequestMessage GetRequestMessage()
            {
                if (!applicationInvoker.IsDefaultOrNull())
                    return applicationInvoker.GetRequest<TResource>().Request;
                if (urlQuery is RequestMessage<TResource>)
                    return (urlQuery as RequestMessage<TResource>).Request;
                return new HttpRequestMessage();
            }
            System.Web.Http.Routing.UrlHelper GetUrlHelper()
            {
                if (!urlHelper.IsDefaultOrNull())
                    return urlHelper;
                if (urlQuery is RequestMessage<TResource>)
                    return new System.Web.Http.Routing.UrlHelper((urlQuery as RequestMessage<TResource>).Request);
                throw new ArgumentException("Could not determine value for urlHelper");
            }
            var validUrlHelper = GetUrlHelper();
            var baseUrl = validUrlHelper.GetLocation(typeof(TResource), routeName);

            var requestMessage = GetRequestMessage();
            requestMessage.RequestUri = baseUrl;
            requestMessage.Method = httpMethod;
            return urlQuery.Compile<TResource, HttpRequestMessage, IFilterApiValues, IFilterApiValues>(
                    requestMessage,
                (request, methodAttr, method, operand) =>
                {
                    return methodAttr.MutateRequest(request,
                        httpMethod, method, operand);
                },
                "Where".PairWithValue((IFilterApiValues)new ResourceQueryExtensions.BinaryComparisonQueryAttribute()));
        }
    }
}
