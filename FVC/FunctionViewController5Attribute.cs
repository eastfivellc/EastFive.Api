using BlackBarLabs.Extensions;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewController5Attribute : FunctionViewController4Attribute
    {
        public override async Task<HttpResponseMessage> CreateResponseAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, string routeName)
        {
            var path = request.RequestUri.Segments
                .Skip(1)
                .Select(segment => segment.Trim('/'.AsArray()))
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();
            var possibleHttpMethods = PossibleHttpMethods(controllerType);
            if (path.Length > 2)
            {
                var actionMethod = path[2];
                var matchingActionKeys = possibleHttpMethods
                    .SelectKeys()
                    .Where(key => String.Compare(key.Method, actionMethod, true) == 0);

                if (matchingActionKeys.Any())
                {
                    var actionHttpMethod = matchingActionKeys.First();
                    var matchingActionMethods = possibleHttpMethods[actionHttpMethod];
                    return await CreateResponseAsync(httpApp, request, routeName, matchingActionMethods,
                        path.Skip(3).ToArray());
                }
            }

            var matchingKey = possibleHttpMethods
                .SelectKeys()
                .Where(key => String.Compare(key.Method, request.Method.Method, true) == 0);

            if (!matchingKey.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            var httpMethod = matchingKey.First();
            var matchingMethods = possibleHttpMethods[httpMethod];

            return await CreateResponseAsync(httpApp, request, routeName, matchingMethods,
                path.Skip(2).ToArray());
        }

        private static async Task<HttpResponseMessage> CreateResponseAsync(IApplication httpApp,
            HttpRequestMessage request, string controllerName, MethodInfo[] methods, string [] fileNameParams)
        {
            #region setup query parameter casting

            var queryParameters = request.RequestUri.ParseQuery()
                .Select(kvp => kvp.Key.ToLower().PairWithValue(kvp.Value))
                .ToDictionary();

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

            #endregion

            #region Get file name from URI (optional part between the controller name and the query)

            CastDelegate<SelectParameterResult> fileNameCastDelegate =
                (query, type, onParsed, onFailure) =>
                {
                    if (!fileNameParams.Any())
                        return onFailure("No URI filename value provided.").AsTask();
                    return httpApp
                        .Bind(type,
                                new QueryParamTokenParser(fileNameParams.First()),
                            v => onParsed(v),
                            (why) => onFailure(why))
                        .AsTask();
                };

            #endregion

            return await httpApp.ParseContentValuesAsync<SelectParameterResult, HttpResponseMessage>(request.Content,
                async (bodyParser, bodyValues) =>
                {
                    CastDelegate<SelectParameterResult> bodyCastDelegate =
                        (queryKey, type, onParsed, onFailure) =>
                        {
                            return bodyParser(queryKey, type,
                                value =>
                                {
                                    var parsedResult = onParsed(value);
                                    return parsedResult;
                                },
                                (why) => onFailure(why));
                        };
                    return await GetResponseAsync(methods,
                        queryCastDelegate,
                        bodyCastDelegate,
                        fileNameCastDelegate,
                        httpApp, request,
                        queryParameters.SelectKeys(),
                        bodyValues,
                        fileNameParams.Any());
                });

        }
    }
}
