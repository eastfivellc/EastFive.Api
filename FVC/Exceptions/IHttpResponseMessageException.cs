using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IHttpResponseMessageException
    {
        HttpResponseMessage CreateResponseAsync(IApplication httpApp,
            HttpRequestMessage request, Dictionary<string, object> queryParameterOptions, 
            MethodInfo method, object[] methodParameters);
    }
}
