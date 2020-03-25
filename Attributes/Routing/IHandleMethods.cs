using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<IHttpResponse> MethodHandlingDelegate(MethodInfo method,
        KeyValuePair<ParameterInfo, object>[] queryParameters,
        IApplication httpApp, IHttpRequest request);

    public interface IHandleMethods
    {
        Task<IHttpResponse> HandleMethodAsync(MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters,
            IApplication httpApp, IHttpRequest request,
            MethodHandlingDelegate continueExecution);
    }
}
