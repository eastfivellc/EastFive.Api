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
    public interface IInstigatable
    {
        Task<HttpResponseMessage> Instigate(
                HttpApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
                ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);
    }

    public interface IInstigate
    {
        bool CanInstigate(ParameterInfo parameterInfo);

        Task<HttpResponseMessage> Instigate(
                HttpApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
                ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);
    }
}
