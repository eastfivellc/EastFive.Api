using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IParseContent
    {
        bool DoesParse(HttpRequestMessage request);

        Task<HttpResponseMessage> ParseContentValuesAsync(
            IApplication httpApp, HttpRequestMessage request,
            Func<
                CastDelegate,
                string[],
                Task<HttpResponseMessage>> onParsedContentValues);
    }
}
