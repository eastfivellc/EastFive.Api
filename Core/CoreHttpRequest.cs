using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.AspNetCore.Http;

using EastFive.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using EastFive.Extensions;

namespace EastFive.Api.Core
{
    public class CoreHttpRequest : IHttpRequest
    {
        public Microsoft.AspNetCore.Http.HttpRequest request;

        public CoreHttpRequest(Microsoft.AspNetCore.Http.HttpRequest request,
            CancellationToken cancellationToken)
        {
            this.request = request;
            this.CancellationToken = cancellationToken;
            this.Properties = new Dictionary<string, object>();
        }

        public IDictionary<string, object> Properties;

        public Uri RequestUri
        {
            get
            {
                return request.GetAbsoluteUri();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public CancellationToken CancellationToken { get; private set; }

        public HttpMethod Method { get; set; }

        public Func<Stream, Task> WriteBody { get; set; }

        public Stream Body => this.request.Body;

        public IFormCollection Form => this.request.Form;

        IDictionary<string, object> IHttpRequest.Properties { get; }

        public IDictionary<string, string[]> Headers => throw new NotImplementedException();

        public IEnumerable<string> GetHeaders(string headerKey)
            => request.GetHeaders(headerKey);

        public void UpdateHeader(string headerKey, Func<string[], string[]> callback)
        {
            throw new NotImplementedException();
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
