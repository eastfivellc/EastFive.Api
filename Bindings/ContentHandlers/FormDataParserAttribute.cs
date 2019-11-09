using EastFive.Api.Bindings;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
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
using System.Xml;

namespace EastFive.Api
{
    public class FormDataParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(HttpRequestMessage request)
        {
            return request.Content.IsFormData();
        }

        public async Task<HttpResponseMessage> ParseContentValuesAsync(
            IApplication httpApp, HttpRequestMessage request,
            Func<
                CastDelegate, 
                string[],
                Task<HttpResponseMessage>> onParsedContentValues)
        {
            var formDataTokens = await ParseOptionalFormDataAsync(request.Content);
            var optionalFormData = formDataTokens.ToDictionary();
            CastDelegate parser =
                (paramInfo, onParsed, onFailure) =>
                {
                    var key = paramInfo
                        .GetAttributeInterface<IBindApiValue>()
                        .GetKey(paramInfo);
                    if (!optionalFormData.ContainsKey(key))
                        return onFailure("Key not found");
                    return paramInfo
                        .Bind(optionalFormData[key], httpApp,
                            (value) => onParsed(value),
                            (why) => onFailure(why));
                };
            return await onParsedContentValues(parser,
                optionalFormData.SelectKeys().ToArray());
        }

        private static async Task<KeyValuePair<string, IParseToken>[]> ParseOptionalFormDataAsync(HttpContent content)
        {
            var formData = await content.ReadAsFormDataAsync();

            var parameters = formData.AllKeys
                .Select(key => key.PairWithValue<string, IParseToken>(
                    new FormDataTokenParser(formData[key])))
                .ToArray();

            return (parameters);
        }
    }
}
