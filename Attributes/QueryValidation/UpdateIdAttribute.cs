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
    public class UpdateIdAttribute : QueryValidationAttribute, IDocumentParameter, IBindJsonApiValue
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

        public Task<TResult> ParseContentDelegateAsync<TResult>(JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo paramInfo,
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(paramInfo);
            var type = paramInfo.ParameterType;
            return PropertyAttribute.ParseJsonContentDelegateAsync(contentJObject,
                    contentString, bindConvert,
                    key, type,
                    httpApp, request,
                onParsed,
                onFailure);
        }
    }
}
