using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Extensions;
using System.Xml;
using EastFive.Linq;
using BlackBarLabs.Extensions;
using System.Xml.Serialization;
using EastFive.Serialization;
using EastFive.Api.Serialization;
using System.IO;
using EastFive.Collections.Generic;

namespace EastFive.Api
{
    public class XmlResourceAttribute : PropertyAttribute, IBindApiValue, IBindXmlApiValue
    {
        public async Task<TResult> ParseContentDelegateAsync<TResult>(XmlDocument xmlDoc,
                ParameterInfo parameterInfo, IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(XmlDocument)))
                return onParsed(xmlDoc);

            return await onFailure($"Cannot bind XML Resource to `{parameterInfo.ParameterType.FullName}.`")
                .AsTask();
        }
    }
}
