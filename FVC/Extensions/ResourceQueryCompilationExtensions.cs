using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Expressions;
using EastFive.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Api
{
    public static class ResourceQueryCompilationExtensions
    {
        public static Uri Location<TResource>(this IQueryable<TResource> urlQuery)
        {
            var baseUrl = BaseUrl(urlQuery);

            var queryUrl = urlQuery.Compile<Uri, IBuildUrls>(
                    baseUrl,
                (url, attr, method, operand) =>
                {
                    var updatedUrl = attr.BindUrlQueryValue(url, method, operand);
                    return updatedUrl;
                },
                (url, method, operand) =>
                {
                    if (method.Name == "Where")
                    {
                        var x = new ResourceQueryExtensions.BinaryComparisonQueryAttribute();
                        return x.BindUrlQueryValue(url, method, operand);
                    }
                    throw new ArgumentException($"Cannot compile Method `{method.DeclaringType.FullName}..{method.Name}`");
                });

            return queryUrl;
        }

        public static HttpRequestMessage CompileRequest<TResource>(this IQueryable<TResource> urlQuery,
            HttpRequestMessage relativeTo = default)
        {
            var httpRequest = GetHttpRequest();
            var baseUrl = BaseUrl(urlQuery);
            httpRequest.RequestUri = baseUrl;

            return urlQuery.Compile<HttpRequestMessage, IBuildHttpRequests>(
                    httpRequest,
                (request, methodAttr, method, operand) =>
                {
                    return methodAttr.MutateRequest(request,
                        method, operand);
                },
                (request, method, operand) =>
                {
                    if (method.Name == "Where")
                    {
                        var x = new ResourceQueryExtensions.BinaryComparisonQueryAttribute();
                        return x.MutateRequest(request, method, operand);
                    }
                    throw new ArgumentException($"Cannot compile Method `{method.DeclaringType.FullName}..{method.Name}`");
                });

            HttpRequestMessage GetHttpRequest()
            {
                if (!relativeTo.IsDefaultOrNull())
                    return relativeTo;

                if (urlQuery is RequestMessage<TResource>)
                    return (urlQuery as RequestMessage<TResource>)
                        .InvokeApplication
                        .GetHttpRequest();

                return new HttpRequestMessage();
            }

        }


        private static Uri BaseUrl<TResource>(IQueryable<TResource> urlQuery)
        {
            var serverUrl = GetServerUrl();
            var prefix = GetRoutePrefix().Trim('/'.AsArray());
            var controllerName = GetControllerName().TrimStart('/'.AsArray());
            Uri.TryCreate($"{serverUrl}/{prefix}/{controllerName}", UriKind.Absolute, out Uri baseUrl);
            return baseUrl;

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
                    .Select(routePrefix => routePrefix.Prefix);
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
                var routeAttrs = typeof(TResource).GetAttributesInterface<IInvokeResource>();
                if (!routeAttrs.Any())
                    throw new ArgumentException($"`{typeof(TResource).FullName}` is not invocable (needs attribute that implements {typeof(IInvokeResource).FullName})");
                return routeAttrs.First().Route;
                //return typeof(TResource).Name
                //    .TrimEnd("Controller",
                //        (trimmedName) => trimmedName,
                //        (originalName) => originalName)
                //    .ToLower();
            }
        }
    }
}
