using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate HttpResponseMessage ResponseHandlingDelegate(ParameterInfo parameterInfo,
            HttpApplication httpApp, HttpRequestMessage request,
            HttpResponseMessage response);

    public interface IHandleResponses
    {
        HttpResponseMessage HandleResponse(ParameterInfo parameterInfo,
            HttpApplication httpApp, HttpRequestMessage request,
            HttpResponseMessage response,
            ResponseHandlingDelegate continueExecution);
    }
}
