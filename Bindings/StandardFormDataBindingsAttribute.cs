using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace EastFive.Api.Bindings
{
    public class StandardFormDataBindingsAttribute :
        Attribute, IBindApiParameter<IFormFile>
    {
        public TResult Bind<TResult>(Type type, IFormFile content, 
            Func<object, TResult> onParsed, 
            Func<string, TResult> onDidNotBind, 
            Func<string, TResult> onBindingFailure)
        {
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = content.OpenReadStream();
                return onParsed((object)streamValue);
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var stream = content.OpenReadStream();
                var bytes = new byte[content.Length];
                stream.Read(bytes, 0, (int)content.Length);
                return onParsed((object)bytes);
            }
            if (type.IsAssignableFrom(typeof(MediaTypeWithQualityHeaderValue)))
            {
                var header = new MediaTypeWithQualityHeaderValue(content.ContentType);
                return onParsed(header);
            }
            if (type.IsAssignableFrom(typeof(ContentDispositionHeaderValue)))
            {
                var header = new ContentDispositionHeaderValue(content.ContentDisposition);
                return onParsed((object)header);
            }
            return onDidNotBind($"{type.FullName} is not supported from Form Data.");
        }
    }
}
