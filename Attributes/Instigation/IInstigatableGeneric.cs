using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{ 
    public interface IInstigatableGeneric
    {
        Task<HttpResponseMessage> InstigatorDelegateGeneric(
            Type type, HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
        Func<object, Task<HttpResponseMessage>> onSuccess);
    }
}
