﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Extensions;

namespace EastFive.Api
{
    public interface IHttpResponse
    {
        IHttpRequest Request { get; }

        HttpStatusCode StatusCode { get; set; }

        string ReasonPhrase { get; set; }

        IDictionary<string, string[]> Headers { get; }

        Task WriteResponseAsync(System.IO.Stream responseStream);
    }

    
}