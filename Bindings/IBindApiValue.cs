using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate SelectParameterResult CastDelegate(ParameterInfo parameterInfo,
        Func<object, SelectParameterResult> onCasted,
        Func<string, SelectParameterResult> onFailedToCast);

    public interface IBindApiValue
    {
        string GetKey(ParameterInfo paramInfo);

        SelectParameterResult TryCast(IApplication httpApp, HttpRequestMessage request,
            MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate fetchQueryParam,
            CastDelegate fetchBodyParam,
            CastDelegate fetchDefaultParam);
    }
}
