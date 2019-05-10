﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IProvideSerialization
    {
        string MediaType { get; }

        HttpResponseMessage Serialize(HttpResponseMessage response, HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo, object obj);
    }
}
