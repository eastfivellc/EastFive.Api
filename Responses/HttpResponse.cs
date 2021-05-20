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
using Microsoft.AspNetCore.Http.Features;

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

        public virtual HttpStatusCode StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IDictionary<string, string[]> Headers { get; private set; }

        private (string, string, TimeSpan?)[] cookies;

        public void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
        {
            this.cookies = cookies
                .NullToEmpty()
                .Append((cookieKey, cookieValue, expireTime))
                .ToArray();
        }

        public virtual Task WriteResponseAsync(HttpContext context)
        {
            WritePreamble(context);
            return WriteResponseAsync(context.Response.Body);
        }

        #region Preamble

        public virtual void WritePreamble(HttpContext context)
        {
            WriteStatusCode(context);
            WriteReason(context);
            WriteHeaders(context);
            WriteCookies(context);
        }

        public virtual void WriteStatusCode(HttpContext context)
        {
            context.Response.StatusCode = (int)this.StatusCode;
        }

        public virtual void WriteReason(HttpContext context)
        {
            WriteReason(context, this.ReasonPhrase);

        }

        public void WriteReason(HttpContext context, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;

            var reasonPhrase = reason.Replace('\n', ';').Replace("\r", "");
            if (reasonPhrase.Length > 510)
                reasonPhrase = new string(reasonPhrase.Take(510).ToArray());

            context.Response.Headers.Add("X-Reason", reasonPhrase);

            var responseFeature = context.Features.Get<IHttpResponseFeature>();
            if (!responseFeature.IsDefaultOrNull())
                responseFeature.ReasonPhrase = reason;

        }

        public virtual void WriteHeaders(HttpContext context)
        {
            foreach (var header in this.Headers)
                context.Response.Headers.Add(header.Key, header.Value);
        }

        public virtual void WriteCookies(HttpContext context)
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

        #endregion

        public virtual Task WriteResponseAsync(System.IO.Stream stream)
        {
            return StatusCode.AsTask();
        }

        
    }
}
