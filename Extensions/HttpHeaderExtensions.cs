using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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

    }
}
