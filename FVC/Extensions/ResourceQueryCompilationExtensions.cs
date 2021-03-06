﻿using BlackBarLabs.Extensions;
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
                (url, attr, method, methodArguments) =>
                {
                    var updatedUrl = attr.BindUrlQueryValue(url, method, methodArguments);
                    return updatedUrl;
                },
                (url, method, methodArguments) =>
                {
                    if (method.Name == "Where")
                    {
                        var x = new ResourceQueryExtensions.BinaryComparisonQueryAttribute();
                        return x.BindUrlQueryValue(url, method, methodArguments);
                    }
                    throw new ArgumentException($"Cannot compile Method `{method.DeclaringType.FullName}..{method.Name}`");
                });

            return queryUrl;
        }

        public static HttpRequestMessage CompileRequest<TResource>(this IQueryable<TResource> urlQuery,
            HttpRequestMessage relativeTo = default)
        {
            var httpRequest = GetHttpRequest();

            return urlQuery.Compile<HttpRequestMessage, IBuildHttpRequests>(
                    httpRequest,
                (request, methodAttr, method, methodArguments) =>
                {
                    return methodAttr.MutateRequest(request,
                        method, methodArguments);
                },
                (request, method, methodArguments) =>
                {
                    if (method.Name == "Where")
                    {
                        var x = new ResourceQueryExtensions.BinaryComparisonQueryAttribute();
                        return x.MutateRequest(request, method, methodArguments);
                    }
                    throw new ArgumentException($"Cannot compile Method `{method.DeclaringType.FullName}..{method.Name}`");
                });

            HttpRequestMessage GetHttpRequest()
            {
                var baseUrl = BaseUrl(urlQuery);
                if (!relativeTo.IsDefaultOrNull())
                {
                    try
                    {
                        relativeTo.RequestUri = baseUrl;  // this will fail if the request has been disposed
                        return relativeTo;
                    }
                    catch { }
                }

                var req = urlQuery is RequestMessage<TResource> 
                    ? (urlQuery as RequestMessage<TResource>)
                        .InvokeApplication
                        .GetHttpRequest()
                    : new HttpRequestMessage();

                req.RequestUri = baseUrl;
                return req;
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
