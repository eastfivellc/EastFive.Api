using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public static class HttpHeaderExtensions
    {
        public static bool IsSuccess(this HttpStatusCode statusCode)
        {
            return ((int)statusCode >= 200) && ((int)statusCode <= 299);
        }

        public static string GetContentMediaTypeNullSafe(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.IsDefaultOrNull())
                return null;
            return httpResponse.Content.GetContentMediaTypeNullSafe();
        }

        public static string GetContentMediaTypeNullSafe(this HttpContent httpContent)
        {
            if (httpContent.IsDefaultOrNull())
                return null;
            return httpContent.Headers.GetContentMediaTypeNullSafe();
        }

        public static string GetContentMediaTypeNullSafe(this HttpContentHeaders headers)
        {
            if (headers.IsDefaultNullOrEmpty())
                return null;
            if (headers.ContentType.IsDefaultOrNull())
                return null;
            return headers.ContentType.MediaType;
        }

        public static string GetFileNameNullSafe(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.IsDefaultOrNull())
                return null;
            return httpResponse.Content.GetFileNameNullSafe();
        }

        public static string GetFileNameNullSafe(this HttpContent httpContent)
        {
            if (httpContent.IsDefaultOrNull())
                return null;
            return httpContent.Headers.GetFileNameNullSafe();
        }

        public static string GetFileNameNullSafe(this HttpContentHeaders headers)
        {
            if (headers.IsDefaultNullOrEmpty())
                return null;
            if (headers.ContentDisposition.IsDefaultOrNull())
                return null;
            return headers.ContentDisposition.FileName;
        }
    }
}
