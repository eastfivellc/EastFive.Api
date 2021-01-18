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
using Microsoft.AspNetCore.Mvc.Razor;

namespace EastFive.Api
{
    public interface IHttpRequest
    {
        Uri RequestUri { get; set; }

        string GetHeader(string headerKey);

        IEnumerable<string> GetHeaders(string headerKey);

        CancellationToken CancellationToken { get; }

        HttpMethod Method { get; set; }

        Func<Stream, Task> WriteBody { get; set; }

        void UpdateHeader(string headerKey, Func<string[], string[]> callback);

        Stream Body { get; }

        bool HasBody { get; }

        bool HasFormContentType { get; }

        IFormCollection Form { get; }

        IDictionary<string, object> Properties { get; }

        IDictionary<string, string[]> Headers { get; }

        IRazorViewEngine RazorViewEngine { get; }

        TResult ReadCookie<TResult>(string cookieKey,
            Func<string, TResult> onCookie,
            Func<TResult> onNotAvailable);
    }

}
