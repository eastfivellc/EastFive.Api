using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Web.Configuration;

namespace EastFive.Api
{
    public class CorsHandlerAttribute : Attribute, IHandleRoutes
    {
        public Task<IHttpResponse> HandleRouteAsync(Type controllerType,
            IApplication httpApp, IHttpRequest request,
            RouteHandlingDelegate continueExecution)
        {
            Func<Task<IHttpResponse>> skip = 
                () => continueExecution(controllerType, httpApp, request);
            
            if(!AppSettings.CorsCorrection.ConfigurationBoolean(s =>s, onNotSpecified:() => false))
                return skip();

            return GetDecoratedResponse();

            Task<IHttpResponse> GetResponse()
            {
                if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                    return skip();

                if (!request.Headers.ContainsKey("Access-Control-Request-Headers"))
                    return skip();

                var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
                return response.AsTask();
            }

            async Task<IHttpResponse> GetDecoratedResponse()
            {
                var response = await GetResponse();
                if (request.Headers.TryGetValue("Origin", out string[] reqOrigins))
                {
                    var respOrigins = UpdateHeader("Access-Control-Allow-Origin", "cors:Origins");
                    var respHeaders = UpdateHeader("Access-Control-Allow-Headers", "cors:Headers");
                    var respMethods = UpdateHeader("Access-Control-Allow-Methods", "cors:Methods");
                }
                return response;

                string UpdateHeader(string headerName, string appSettingName)
                {
                    request.Headers.TryGetValue(headerName, out string[] values);
                    return appSettingName.ConfigurationString(
                        (appSettingValue) =>
                        {
                            if (headerName == "Access-Control-Allow-Origin")
                            {
                                // accept localhost (all ports)
                                // accept request server
                                // accept any additional servers in config
                                var localhostAuthorities = reqOrigins
                                    .NullToEmpty()
                                    .SelectMany(reqOrigin => reqOrigin.Split(','.AsArray(), StringSplitOptions.RemoveEmptyEntries))
                                    .Where(
                                        (reqOrigin) =>
                                        {
                                            if (!Uri.TryCreate(reqOrigin, UriKind.Absolute, out Uri reqOriginUri))
                                                return false;

                                            return reqOriginUri.GetLeftPart(UriPartial.Authority).IndexOf("localhost", StringComparison.OrdinalIgnoreCase) != -1;
                                        });
                                var requestAuthority = request.RequestUri.GetLeftPart(UriPartial.Authority);
                                var allowableOriginValues = values
                                    .NullToEmpty()
                                    .Where(value => value != "*")
                                    .Concat(localhostAuthorities)
                                    .Append(requestAuthority)
                                    .Concat(appSettingValue.Split(','.AsArray(), StringSplitOptions.RemoveEmptyEntries))
                                    .Distinct()
                                    .ToArray();
                                var allowedOrigin = allowableOriginValues.First(
                                    (allowed, next) =>
                                    {
                                        if (!reqOrigins.Contains(allowed, StringComparer.OrdinalIgnoreCase))
                                            return next();

                                        return allowed;
                                    },
                                    () => requestAuthority);
                                response.Headers.Remove(headerName);
                                response.SetHeader(headerName, allowedOrigin);
                                return allowedOrigin;
                            }

                            var updatedValues = values
                                .NullToEmpty()
                                .SelectMany(value => value.Split(','.AsArray(), StringSplitOptions.RemoveEmptyEntries))
                                .Where(value => value != "*")
                                .Append(appSettingValue)
                                .Distinct()
                                .ToArray()
                                .Join(",");
                            response.Headers.Remove(headerName);
                            response.SetHeader(headerName, updatedValues);
                            return updatedValues;
                        },
                        (why) => values
                            .NullToEmpty()
                            .ToArray()
                            .Join(","));
                }
            }

            //if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
            //    return skip();

            //if (!request.TryGetHeader("Access-Control-Request-Headers", out string accessControlRequestHeader))
            //    return skip();

            //var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
            //if (!response.Headers.ContainsKey("Access-Control-Allow-Origin"))
            //    response.SetHeader("Access-Control-Allow-Origin", "*");
            //if (!response.Headers.ContainsKey("Access-Control-Allow-Headers"))
            //    response.SetHeader("Access-Control-Allow-Headers", "*");
            //if (!response.Headers.ContainsKey("Access-Control-Allow-Methods"))
            //    response.SetHeader("Access-Control-Allow-Methods", "*");

            //return response.AsTask();
        }
    }
}
