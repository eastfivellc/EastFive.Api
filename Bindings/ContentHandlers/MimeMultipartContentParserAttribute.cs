using EastFive.Api.Bindings;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Web;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Api
{
    public interface IBindMultipartApiValue
    {
        TResult ParseContentDelegate<TResult>(IDictionary<string, MultipartContentTokenParser> content,
                ParameterInfo parameterInfo,
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class MimeMultipartContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(HttpRequestMessage request)
        {
            if (request.Content.IsDefaultOrNull())
                return false;
            return request.Content.IsMimeMultipartContent();
        }

        public async Task<HttpResponseMessage> ParseContentValuesAsync(
            IApplication httpApp, HttpRequestMessage request,
            Func<
                CastDelegate, 
                string[],
                Task<HttpResponseMessage>> onParsedContentValues)
        {
            var streamProvider = new MultipartMemoryStreamProvider();
            try
            {
                await request.Content.ReadAsMultipartAsync(streamProvider);
            }
            catch (IOException readError)
            {
                CastDelegate exceptionParser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        if (readError.InnerException.IsDefaultOrNull())
                            return onFailure(readError.Message);

                        var failMsg = $"{readError.Message}:{readError.InnerException.Message}";
                        return onFailure(failMsg);
                    };
                return await onParsedContentValues(exceptionParser, new string[] { });
            }
            var contentsLookup = await streamProvider.Contents
                .Select(
                    async (file) =>
                    {
                        if (file.IsDefaultOrNull())
                            return default;

                        var key = file.Headers.ContentDisposition.Name.Trim(new char[] { '"' });
                        var fileNameMaybe = file.Headers.ContentDisposition.FileName;
                        if (null != fileNameMaybe)
                            fileNameMaybe = fileNameMaybe.Trim(new char[] { '"' });
                        var contents = await file.ReadAsByteArrayAsync();

                        var kvp = key.PairWithValue(
                                new MultipartContentTokenParser(file, contents, fileNameMaybe));
                        return kvp.AsOptional();
                    })
                .AsyncEnumerable()
                .SelectWhereHasValue()
                .ToDictionaryAsync();

            CastDelegate parser =
                (paramInfo, onParsed, onFailure) =>
                {
                    return paramInfo
                        .GetAttributeInterface<IBindMultipartApiValue>(true)
                        .ParseContentDelegate(contentsLookup,
                                paramInfo, httpApp, request,
                            onParsed,
                            onFailure);
                };
            return await onParsedContentValues(parser, contentsLookup.SelectKeys().ToArray());
        }

    }

    public class MultipartContentTokenParser : IParseToken
    {
        private byte[] contents;
        private string fileNameMaybe;
        private HttpContent file;

        public class MemoryStreamForFile : MemoryStream
        {
            public MemoryStreamForFile(byte[] buffer) : base(buffer) { }
            public string FileName { get; set; }
        }

        public MultipartContentTokenParser(HttpContent file, byte[] contents, string fileNameMaybe)
        {
            this.file = file;
            this.contents = contents;
            this.fileNameMaybe = fileNameMaybe;
        }

        public IParseToken[] ReadArray()
        {
            throw new NotImplementedException();
        }

        public byte[] ReadBytes()
        {
            return contents;
        }

        public IDictionary<string, IParseToken> ReadDictionary()
        {
            throw new NotImplementedException();
        }

        public T ReadObject<T>()
        {
            if (typeof(HttpContent) == typeof(T))
            {
                return (T)((object)this.file);
            }
            //if (typeof(ByteArrayContent) == typeof(T))
            //{
            //    if(this.file is ByteArrayContent)
            //        return (T)((object)this.file);
            //    var byteArrayContent = new ByteArrayContent(this.file.)
            //    {
            //        Headers
            //    }
            //}
            throw new NotImplementedException();
        }
        public object ReadObject()
        {
            throw new NotImplementedException();
        }

        public Stream ReadStream()
        {
            return new MemoryStreamForFile(contents)
            { FileName = fileNameMaybe };
        }

        public bool IsString => true;
        public string ReadString()
        {
            return System.Text.Encoding.UTF8.GetString(contents);
        }
    }
}
