using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<HttpResponseMessage> ValidateHttpDelegate(
        KeyValuePair<ParameterInfo, object>[] parameters, MethodInfo method,
        IApplication httpApp, HttpRequestMessage request);

    public interface IValidateHttpRequest
    {
        Task<HttpResponseMessage> ValidateRequest(SelectParameterResult parameterSelection, 
            MethodInfo methodCurrent, 
            IApplication httpAppCurrent, HttpRequestMessage requestCurrent,
            ValidateHttpDelegate boundCallback);
    }
}
