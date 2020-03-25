using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;

namespace EastFive.Api
{
    public interface IHttpRequest
    {
        Uri AbsoluteUri { get; }

        IEnumerable<string> GetHeaders(string headerKey);

        CancellationToken CancellationToken { get; }

        void UpdateHeader(string headerKey, Func<string[], string[]> callback);
    }

}
