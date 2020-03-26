using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq.Async;
using Newtonsoft.Json;

namespace EastFive.Api
{
    public class EnumerableAsyncHttpResponse<T> : HttpResponse
    {
        private IEnumerableAsync<T> objectsAsync;
        private IApplication application;
        private ParameterInfo parameterInfo;

        public EnumerableAsyncHttpResponse(IApplication application,
            IHttpRequest request, ParameterInfo parameterInfo, HttpStatusCode statusCode,
            IEnumerableAsync<T> objectsAsync)
            : base(request, statusCode)
        {
            this.application = application;
            this.objectsAsync = objectsAsync;
            this.parameterInfo = parameterInfo;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            var streamWriter = 
                this.Request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
                    new StreamWriter(responseStream, writerEncoding)
                    :
                    new StreamWriter(responseStream);
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new Serialization.Converter());
            settings.DefaultValueHandling = DefaultValueHandling.Ignore;

            var enumerator = objectsAsync.GetEnumerator();
            await streamWriter.WriteAsync('[');
            bool first = true;
            while (await enumerator.MoveNextAsync())
            {
                if(!first)
                    await streamWriter.WriteAsync(',');
                first = false;

                var obj = enumerator.Current;
                var objType = obj.GetType();
                if (!objType.ContainsAttributeInterface<IProvideSerialization>())
                {
                    var contentJsonString = JsonConvert.SerializeObject(obj, settings);
                    await streamWriter.WriteAsync(contentJsonString);
                    continue;
                }

                var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
                await serializationProvider.SerializeAsync(responseStream,
                    application, this.Request, this.parameterInfo, obj);
            }
            await streamWriter.WriteAsync(']');
        }
    }
}
