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
    public interface IInvokeResource
    {
        string Route { get; }

        string ContentType { get; }

        Type Resource { get; }

        Task<HttpResponseMessage> CreateResponseAsync(Type controllerType, 
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            string routeName);
    }

    public interface IInvokeExtensions
    {
        KeyValuePair<Type, MethodInfo>[] GetResourcesExtended(Type extensionType);
    }
}
