using EastFive.Api.Serialization;
using EastFive.Extensions;
using EastFive.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class XmlContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(HttpRequestMessage request)
        {
            return request.Content.IsXml();
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
                        var failMsg = $"[{key}] was not provided (XML body content was empty).";
                        return onFailure(failMsg);
                    };
                return await onParsedContentValues(emptyParser, exceptionKeys);
            }

            var xmldoc = new XmlDocument();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore, // prevents XXE attacks, such as Billion Laughs
                MaxCharactersFromEntities = 1024,
                XmlResolver = null,                   // prevents external entity DoS attacks, such as slow loading links or large file requests
            };
            try
            {
                using (var strReader = new StringReader(contentString))
                using (var xmlReader = XmlReader.Create(strReader, settings))
                {
                    xmldoc.Load(xmlReader);
                }
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
