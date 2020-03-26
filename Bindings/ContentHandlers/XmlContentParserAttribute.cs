using EastFive.Api.Core;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using EastFive.Web;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Api
{
    public interface IBindXmlApiValue
    {
        TResult ParseContentDelegate<TResult>(
                XmlDocument xmlDoc, string rawContent,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class XmlContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest request)
        {
            return request.IsXml();
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest request,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
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
                        var failMsg = $"[{key}] was not provided (XML body content was empty).";
                        return onFailure(failMsg);
                    };
                return await onParsedContentValues(emptyParser, exceptionKeys);
            }

            var xmldoc = new XmlDocument();
            try
            {
                xmldoc.LoadXml(contentString);
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
                        .GetAttributeInterface<IBindXmlApiValue>()
                        .ParseContentDelegate(xmldoc, contentString,
                            paramInfo, httpApp, request,
                            onParsed,
                            onFailure);
                };
            return await onParsedContentValues(parser, new string[] { });
        }
    }
}
