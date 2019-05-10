using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static EastFive.Api.HttpApplication;

namespace EastFive.Api
{
    public interface IInvokeApplication
    {
        Uri ServerLocation { get; }

        IDictionary<string, string> Headers { get; }

        RequestMessage<TResource> GetRequest<TResource>();

    }
}
