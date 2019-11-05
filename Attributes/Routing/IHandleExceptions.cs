using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<HttpResponseMessage> HandleExceptionDelegate(Exception ex,
        MethodInfo method,
        KeyValuePair<ParameterInfo, object>[] queryParameters,
        IApplication httpApp, HttpRequestMessage request);

    public interface IHandleExceptions
    {
        Task<HttpResponseMessage> HandleExceptionAsync(Exception ex, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters,
            IApplication httpApp, HttpRequestMessage request,
            HandleExceptionDelegate continueExecution);
    }
}
