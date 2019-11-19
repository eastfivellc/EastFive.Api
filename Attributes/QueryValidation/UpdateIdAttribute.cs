using EastFive.Api.Resources;
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
    public class UpdateIdAttribute : QueryValidationAttribute, IDocumentParameter,
        IBindJsonApiValue, IBindMultipartApiValue
    {
        public override string Name
        {
            get
            {
                var name = base.Name;
                if (name.HasBlackSpace())
                    return name;
                return "id";
            }
            set => base.Name = value;
        }

        public Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter()
            {
                Default = true,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "QUERY|BODY",
            };
        }

        public TResult ParseContentDelegate<TResult>(JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo paramInfo,
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(paramInfo);
            return PropertyAttribute.ParseJsonContentDelegate(contentJObject,
                    contentString, bindConvert,
                    key, paramInfo,
                    httpApp, request,
                onParsed,
                onFailure);
        }

        public TResult ParseContentDelegate<TResult>(IDictionary<string, MultipartContentTokenParser> contentsLookup, 
                ParameterInfo parameterInfo, IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            if (!contentsLookup.ContainsKey(key))
                return onFailure("Key not found");

            var type = parameterInfo.ParameterType;
            return PropertyAttribute.ContentToType(httpApp, parameterInfo, contentsLookup[key],
                    onParsed,
                    onFailure);
        }

    }
}
