using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{ 
    public interface IInstigatableGeneric
    {
        Task<HttpResponseMessage> InstigatorDelegateGeneric(
            Type type, IApplication httpApp, HttpRequestMessage request,
            CancellationToken cancellationToken,
            ParameterInfo parameterInfo,
        Func<object, Task<HttpResponseMessage>> onSuccess);
    }

    public interface IInstigateGeneric
    {
        bool CanInstigate(ParameterInfo parameterInfo);

        Task<HttpResponseMessage> InstigatorDelegateGeneric(
            Type type, IApplication httpApp, HttpRequestMessage request,
            CancellationToken cancellationToken, 
            ParameterInfo parameterInfo,
        Func<object, Task<HttpResponseMessage>> onSuccess);
    }
}
