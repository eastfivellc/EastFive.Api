using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<HttpResponseMessage> MethodHandlingDelegate(MethodInfo method,
        KeyValuePair<ParameterInfo, object>[] queryParameters,
        IApplication httpApp, HttpRequestMessage request);

    public interface IHandleMethods
    {
        Task<HttpResponseMessage> RouteHandlersAsync(MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters,
            IApplication httpApp, HttpRequestMessage request,
            MethodHandlingDelegate continueExecution);
    }
}
