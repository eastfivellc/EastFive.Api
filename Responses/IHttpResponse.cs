using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Extensions;
using System.IO;

namespace EastFive.Api
{
    public interface IHttpResponse
    {
        IHttpRequest Request { get; }

        HttpStatusCode StatusCode { get; set; }

        string ReasonPhrase { get; set; }

        IDictionary<string, string[]> Headers { get; }

        void WriteCookie(string cookieKey, string cookieValue, TimeSpan? expireTime);

        void WriteCookiesToResponse(HttpContext context);

        Task WriteResponseAsync(Stream stream);
    }

    
}
