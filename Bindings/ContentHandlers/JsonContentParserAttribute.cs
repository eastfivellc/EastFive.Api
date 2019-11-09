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
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class JsonContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(HttpRequestMessage request)
        {
            return request.Content.IsJson();
        }

        public async Task<HttpResponseMessage> ParseContentValuesAsync(
            IApplication httpApp, HttpRequestMessage request,
            Func<
                CastDelegate, 
                string[],
                Task<HttpResponseMessage>> onParsedContentValues)
        {
            var content = request.Content;
            var contentString = await content.ReadAsStringAsync();
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
            JObject contentJObject;
            try
            {
                contentJObject = Newtonsoft.Json.Linq.JObject.Parse(contentString);
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
            CastDelegate parser =
                (paramInfo, onParsed, onFailure) =>
                {
                    return paramInfo
                        .GetAttributeInterface<IBindJsonApiValue>()
                        .ParseContentDelegate(contentJObject,
                                contentString, bindConvert,
                                paramInfo, httpApp, request,
                            onParsed,
                            onFailure);
                };
            var keys = contentJObject
                    .Properties()
                    .Select(jProperty => jProperty.Name)
                    .ToArray();
            return await onParsedContentValues(parser, keys);
        }
    }
}
