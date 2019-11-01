using EastFive.Api.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class InstigatableResponseAttribute : Attribute
    {
        protected HttpResponseMessage UpdateResponse(ParameterInfo parameterInfo,
            HttpApplication httpApp, HttpRequestMessage request,
            HttpResponseMessage response)
        {
            response.Headers.Add(ControllerHandler.HeaderStatusName, parameterInfo.ParameterType.FullName);
            response.Headers.Add(ControllerHandler.HeaderStatusInstance, parameterInfo.Name);
            return httpApp.GetType()
                .GetAttributesInterface<IHandleResponses>(true, true)
                .Aggregate<IHandleResponses, ResponseHandlingDelegate>(
                    (param, app, req, responseFinal) => responseFinal,
                    (callback, responseHandler) =>
                    {
                        return (param, app, req, resp) =>
                            responseHandler.HandleResponse(param,
                                app, req, resp,
                                callback);
                    })
                .Invoke(parameterInfo, httpApp, request, response);
        }
    }
}
