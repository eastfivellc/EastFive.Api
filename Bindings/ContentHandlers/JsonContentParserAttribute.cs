using EastFive.Api.Core;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using EastFive.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IBindJsonApiValue
    {
        TResult ParseContentDelegate<TResult>(Newtonsoft.Json.Linq.JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class JsonContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest routeData)
        {
            var request = routeData.request;
            return request.IsJson();
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest routeData,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
            var request = routeData.request;
            var contentString = request.Body.ReadAsString();
            var exceptionKeys = new string[] { };
            if (contentString.IsNullOrWhiteSpace())
            {
                CastDelegate emptyParser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        var key = paramInfo
                            .GetAttributeInterface<IBindApiValue>()
                            .GetKey(paramInfo)
                            .ToLower();
                        var type = paramInfo.ParameterType;
                        return onFailure($"[{key}] was not provided (JSON body content was empty).");
                    };
                return await onParsedContentValues(emptyParser, exceptionKeys);
            }
            var bindConvert = new BindConvert(httpApp as HttpApplication);
            try
            {
                var contentJObject = Newtonsoft.Json.Linq.JObject.Parse(contentString);
                CastDelegate parser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        return paramInfo
                            .GetAttributeInterface<IBindJsonApiValue>()
                            .ParseContentDelegate(contentJObject,
                                    contentString, bindConvert,
                                    paramInfo, httpApp, routeData,
                                onParsed,
                                onFailure);
                    };
                var keys = contentJObject
                        .Properties()
                        .Select(jProperty => jProperty.Name)
                        .ToArray();
                return await onParsedContentValues(parser, keys);
            }
            catch (Exception ex)
            {
                CastDelegate exceptionParser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        return onFailure(ex.Message);
                    };
                return await onParsedContentValues(exceptionParser, exceptionKeys);
            }
        }
    }
}
