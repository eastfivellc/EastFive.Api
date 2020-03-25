using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<IHttpResponse> ValidateHttpDelegate(
        KeyValuePair<ParameterInfo, object>[] parameters, MethodInfo method,
        IApplication httpApp, IHttpRequest routeData);

    public interface IValidateHttpRequest
    {
        Task<IHttpResponse> ValidateRequest(
            KeyValuePair<ParameterInfo, object>[] parameterSelection, 
            MethodInfo methodCurrent, 
            IApplication httpApp, IHttpRequest routeData,
            ValidateHttpDelegate bound);
    }
}
