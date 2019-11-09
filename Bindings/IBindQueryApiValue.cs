using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IBindQueryApiValue
    {
        TResult ParseContentDelegate<TResult>(Newtonsoft.Json.Linq.JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo parameterInfo,
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }
}
