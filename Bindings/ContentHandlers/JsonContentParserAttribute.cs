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
        TResult ParseContentDelegate<TResult>(Newtonsoft.Json.Linq.JContainer contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class JsonContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest request)
        {
            return request.IsJson();
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest request,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
            if (!request.HasBody)
                return await BodyMissing("Body was not provided");

            var contentString = await request.ReadContentAsStringAsync();

            if (contentString.IsNullOrWhiteSpace())
                return await BodyMissing("JSON body content is empty");

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
            catch (Newtonsoft.Json.JsonReaderException)
            {
                try
                {
                    var contentJArray = Newtonsoft.Json.Linq.JArray.Parse(contentString);
                    CastDelegate parser =
                        (paramInfo, onParsed, onFailure) =>
                        {
                            return paramInfo
                                .GetAttributeInterface<IBindJsonApiValue>()
                                .ParseContentDelegate(contentJArray,
                                        contentString, bindConvert,
                                        paramInfo, httpApp, request,
                                    onParsed,
                                    onFailure);
                        };
                    var keys = new string[] { };
                    return await onParsedContentValues(parser, keys);
                }
                catch (Exception ex)
                {
                    return await BodyMissing(ex.Message);
                }
            }
            catch (Exception ex)
            {
                return await BodyMissing(ex.Message);
            }

            Task<IHttpResponse> BodyMissing(string failureMessage)
            {
                CastDelegate emptyParser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        var key = paramInfo
                            .GetAttributeInterface<IBindApiValue>()
                            .GetKey(paramInfo)
                            .ToLowerNullSafe();
                        var type = paramInfo.ParameterType;
                        return onFailure($"[{key}] could not be parsed ({failureMessage}).");
                    };
                var exceptionKeys = new string[] { };
                return onParsedContentValues(emptyParser, exceptionKeys);
            }
        }
    }
}
