using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.AspNetCore.Http;

using EastFive.Linq;
using EastFive.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using EastFive.Extensions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Primitives;

namespace EastFive.Api.Core
{
    public class CoreHttpRequest : IHttpRequest
    {
        private Uri uri;
        public Microsoft.AspNetCore.Http.HttpRequest request;

        public CoreHttpRequest(Microsoft.AspNetCore.Http.HttpRequest request,
            IRazorViewEngine razorViewEngine,
            CancellationToken cancellationToken)
        {
            this.uri = request.GetAbsoluteUri();
            this.request = request;
            this.CancellationToken = cancellationToken;
            this.Properties = new Dictionary<string, object>();
            this.Headers = request.Headers
                .Select(kvp => kvp.Key.PairWithValue((string[])kvp.Value))
                .ToDictionary();
            this.Method = new HttpMethod(request.Method);
            this.RazorViewEngine = razorViewEngine;
        }

        public IRequestHeaders RequestHeaders => new CoreRequestHeaders(
            new HeaderDictionary(
                this.Headers
                    .Select(kvp => kvp.Key.PairWithValue(new StringValues(kvp.Value)))
                    .ToDictionary()));

        public class CoreRequestHeaders : Microsoft.AspNetCore.Http.Headers.RequestHeaders, IRequestHeaders
        {
            public CoreRequestHeaders(IHeaderDictionary headers)
                :
                base(headers)
            {

            }
        }

        public IDictionary<string, object> Properties { get; private set; }

        public Uri RequestUri
        {
            get
            {
                return this.uri;
            }
            set
            {
                this.uri = value;
            }
        }

        public CancellationToken CancellationToken { get; private set; }

        public HttpMethod Method { get; set; }

        public Func<Stream, Task> WriteBody { get; set; }

        public Stream Body => this.request.Body;

        public bool HasBody => this.request.ContentLength.HasValue;

        public bool HasFormContentType => this.request.HasFormContentType;

        public IFormCollection Form => this.request.HasFormContentType?
            this.request.Form
            :
            default;


        public IDictionary<string, string[]> Headers { get; private set; }

        public IRazorViewEngine RazorViewEngine { get; private set; }

        public string GetHeader(string headerKey) => Headers
            .Where(kvp => kvp.Key.ToLower() == headerKey.ToLower())
            .First(
                (v, next) => v.Value.Any() ? v.Value.First() : string.Empty,
                () => string.Empty);

        public IEnumerable<string> GetHeaders(string headerKey) => Headers
            .Where(kvp => kvp.Key.ToLower() == headerKey.ToLower())
            .SelectMany(kvp => kvp.Value.ToArray())
            .SelectMany(header => header.Split(','))
            .Select(header => header.Trim());

        public void UpdateHeader(string headerKey, Func<string[], string[]> callback)
        {
            Headers.TryGetValue(headerKey, out string[] headers);
            Headers[headerKey] = callback(headers.NullToEmpty().ToArray());
        }

        public TResult ReadCookie<TResult>(string cookieKey, Func<string, TResult> onCookie, Func<TResult> onNotAvailable)
        {
            return request.Cookies
                .Where(cookie => cookie.Key == cookieKey)
                .First(
                    (cookie, next) => onCookie(cookie.Value),
                    onNotAvailable);
        }
    }

    static class CoreHttpRequestExtensions
    {
        public static string GetHeader(this Microsoft.AspNetCore.Http.HttpRequest req, string headerKey)
            => req.Headers
            .Where(kvp => kvp.Key.ToLower() == headerKey.ToLower())
            .First(
                (v, next) => v.Value.Any() ? v.Value.First() : string.Empty,
                () => string.Empty);

        public static IEnumerable<string> GetHeaders(this Microsoft.AspNetCore.Http.HttpRequest req, string headerKey)
            => req.Headers
            .Where(kvp => kvp.Key.ToLower() == headerKey.ToLower())
            .SelectMany(kvp => kvp.Value.ToArray());

        public static string GetMediaType(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetHeaders("content-type")
            .First(
                (v, next) => v,
                () => string.Empty);

        public static string GetAuthorization(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetHeaders("Authorization")
            .First(
                (v, next) => v,
                () => string.Empty);

        public static IEnumerable<MediaTypeWithQualityHeaderValue> GetAcceptTypes(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetHeaders("accept")
            .Select(acceptString => new MediaTypeWithQualityHeaderValue(acceptString));

        public static IEnumerable<StringWithQualityHeaderValue> GetAcceptLanguage(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetHeaders("Accept-Language")
            .Select(acceptString => new StringWithQualityHeaderValue(acceptString));

        public static bool IsJson(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetMediaType().ToLower().Contains("json");

        public static bool IsMimeMultipartContent(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetMediaType().ToLower().StartsWith("multipart/");

        public static bool IsXml(this Microsoft.AspNetCore.Http.HttpRequest req)
            => req.GetMediaType().ToLower().Contains("xml");

    }
}
