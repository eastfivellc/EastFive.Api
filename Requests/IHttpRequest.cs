using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public interface IHttpRequest
    {
        Uri RequestUri { get; set; }

        IEnumerable<string> GetHeaders(string headerKey);

        CancellationToken CancellationToken { get; }

        HttpMethod Method { get; set; }

        Func<Stream, Task> WriteBody { get; set; }

        void UpdateHeader(string headerKey, Func<string[], string[]> callback);

        Stream Body { get; }

        IFormCollection Form { get; }

        IDictionary<string, object> Properties { get; }

        IDictionary<string, string[]> Headers { get; }
    }

}
