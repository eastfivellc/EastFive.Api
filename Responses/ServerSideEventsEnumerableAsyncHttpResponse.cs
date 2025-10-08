﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Headers;

using Newtonsoft.Json;

using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Serialization;

namespace EastFive.Api
{
    public class ServerSideEventsEnumerableAsyncHttpResponse<T> : HttpResponse
    {
        private IEnumerableAsync<T> objectsAsync;
        private  Func<T, Task> onCompleted;
        private IApplication application;
        private ParameterInfo parameterInfo;

        public ServerSideEventsEnumerableAsyncHttpResponse(IApplication application,
            IHttpRequest request, ParameterInfo parameterInfo, HttpStatusCode statusCode,
            IEnumerableAsync<T> objectsAsync, Func<T, Task> onCompleted = null)
            : base(request, statusCode)
        {
            this.application = application;
            this.objectsAsync = objectsAsync;
            this.parameterInfo = parameterInfo;
            this.onCompleted = onCompleted;
        }

        public override void WriteHeaders(Microsoft.AspNetCore.Http.HttpContext context, ResponseHeaders headers)
        {
            base.WriteHeaders(context, headers);
            headers.ContentType = new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue() { NoCache = true };
            headers.Append("X-Accel-Buffering", "no");
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            using (var streamWriter =
                this.Request.TryGetAcceptCharset(out Encoding writerEncoding) ?
                    new StreamWriter(responseStream, writerEncoding)
                    :
                    new StreamWriter(responseStream, new UTF8Encoding(false)))
            {
                streamWriter.AutoFlush = true;
                
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new Serialization.Converter(this.Request));
                settings.DefaultValueHandling = DefaultValueHandling.Include;

                var enumerator = objectsAsync.GetEnumerator();
                T obj = default;
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        obj = enumerator.Current;
                        var objType = (obj == null) ?
                            typeof(T)
                            :
                            obj.GetType();

                        await streamWriter.WriteAsync("data: ");

                        if (!objType.ContainsAttributeInterface<IProvideSerialization>())
                        {
                            var contentJsonString = JsonConvert.SerializeObject(obj, settings);
                            await streamWriter.WriteAsync(contentJsonString);
                            continue;
                        }

                        var serializationProvider = objType
                                .GetAttributesInterface<IProvideSerialization>()
                                .OrderByDescending(x => x.GetPreference(this.Request))
                                .First();

                        using (var memoryStream = new MemoryStream())
                        {
                            await serializationProvider.SerializeAsync(memoryStream,
                                application, this.Request, this.parameterInfo, obj);
                            var responseString = memoryStream.ToArray().GetString(streamWriter.Encoding);
                            await streamWriter.WriteAsync(responseString);
                        }

                        await streamWriter.WriteAsync("\n\n");
                    }
                }
                catch (Exception ex)
                {
                    await streamWriter.WriteAsync($"event: error\ndata: {JsonConvert.SerializeObject(new { error = ex.Message }, settings)}\n\n");
                }
                finally
                {
                    await streamWriter.WriteAsync("event: complete\ndata: {\"status\":\"completed\"}\n\n");
                    if (onCompleted != null)
                        await onCompleted(obj);
                }
            }
        }
    }
}
