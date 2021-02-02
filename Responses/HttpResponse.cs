using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Collections.Generic;

namespace EastFive.Api
{
    public class HttpResponse : IHttpResponse
    {
        public HttpResponse(IHttpRequest request, HttpStatusCode statusCode)
        {
            this.Request = request;
            this.StatusCode = statusCode;
            this.ReasonPhrase = string.Empty;
            this.Headers = new Dictionary<string, string[]>();
        }

        public IHttpRequest Request { get; private set; }

        public HttpStatusCode StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IDictionary<string, string[]> Headers { get; private set; }

        private (string, string, TimeSpan?)[] cookies;

        public void WriteCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
        {
            this.cookies = cookies
                .NullToEmpty()
                .Append((cookieKey, cookieValue, expireTime))
                .ToArray();
        }

        public void WriteCookiesToResponse(HttpContext context)
        {
            if (cookies.IsDefaultNullOrEmpty())
                return;
            foreach (var (cookieKey, cookieValue, expireTime) in cookies)
            {
                CookieOptions option = new CookieOptions();

                if (expireTime.HasValue)
                    option.Expires = DateTime.Now + expireTime.Value;
                else
                    option.Expires = DateTime.Now.AddMilliseconds(10);

                context.Response.Cookies.Append(cookieKey, cookieValue, option);
            }
        }

        public virtual Task WriteResponseAsync(System.IO.Stream stream)
        {
            return StatusCode.AsTask();
        }
    }
}
