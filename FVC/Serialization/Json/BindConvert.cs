using EastFive.Collections.Generic;
using EastFive.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Serialization
{
    public class BindConvert : Newtonsoft.Json.JsonConverter
    {
        HttpApplication application;

        private class JsonReaderTokenParser : IParseToken
        {
            private JsonReader reader;

            public JsonReaderTokenParser(JsonReader reader)
            {
                this.reader = reader;
            }


            public bool IsString
            {
                get
                {
                    if (reader.TokenType == JsonToken.String)
                        return true;
                    //if (reader.TokenType == JsonToken.Boolean)
                    //    return true;
                    //if (reader.TokenType == JsonToken.Null)
                    //    return true;
                    return false;
                }
            }

            public string ReadString()
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var value = (string)reader.Value;
                    return value;
                }
                if (reader.TokenType == JsonToken.Boolean)
                {
                    var valueBool = (bool)reader.Value;
                    var value = valueBool.ToString();
                    return value;
                }
                if (reader.TokenType == JsonToken.Integer)
                {
                    var valueInt = (long)reader.Value;
                    var value = valueInt.ToString();
                    return value;
                }
                if (reader.TokenType == JsonToken.Date)
                {
                    var valueDate = (DateTime)reader.Value;
                    var value = valueDate.ToString();
                    return value;
                }
                if (reader.TokenType == JsonToken.Null)
                {
                    return (string)null;
                }
                if (reader.TokenType == JsonToken.StartArray)
                {
                    return (string)null;
                }
                throw new Exception($"BindConvert does not handle token type: {reader.TokenType}");
            }

            public T ReadObject<T>()
            {
                var token = JToken.Load(reader);
                if (token is JValue)
                {
                    return token.Value<T>();
                }

                return token.ToObject<T>();
            }


            public object ReadObject()
            {
                var token = JToken.Load(reader);
                if (token is JValue)
                {
                    if (token.Type == JTokenType.Boolean)
                        return token.Value<bool>();
                    if (token.Type == JTokenType.Bytes)
                        return token.Value<byte[]>();
                    if (token.Type == JTokenType.Date)
                        return token.Value<DateTime>();
                    if (token.Type == JTokenType.Float)
                        return token.Value<float>();
                    if (token.Type == JTokenType.Guid)
                        return token.Value<Guid>();
                    if (token.Type == JTokenType.Integer)
                        return token.Value<int>();
                    if (token.Type == JTokenType.None)
                        return null;
                    if (token.Type == JTokenType.Null)
                        return null;
                    if (token.Type == JTokenType.String)
                        return token.Value<string>();
                    if (token.Type == JTokenType.Uri)
                        return token.Value<Uri>();
                }
                return token.ToObject<object>();
            }

            public IParseToken[] ReadArray()
            {
                var token = JToken.Load(reader);
                var tokenParser = new JsonTokenParser(token);
                return tokenParser.ReadArray();
            }

            public IDictionary<string, IParseToken> ReadDictionary()
            {
                var token = JToken.Load(reader);
                var tokenParser = new JsonTokenParser(token);
                return tokenParser.ReadDictionary();
            }

            public byte[] ReadBytes()
            {
                throw new NotImplementedException();
            }

            public Stream ReadStream()
            {
                throw new NotImplementedException();
            }
        }

        public BindConvert(HttpApplication httpApplication)
        {
            this.application = httpApplication;
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            return this.application.CanBind(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return this.application.Bind(objectType, new JsonReaderTokenParser(reader),
                v => v,
                (why) => existingValue);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("BindConvert cannot write values.");
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

    public class JsonTokenParser : IParseToken
    {
        private JToken valueToken;

        public JsonTokenParser(JToken valueToken)
        {
            this.valueToken = valueToken;
        }

        public byte[] ReadBytes()
        {
            return valueToken.ToObject<byte[]>();
        }

        public Stream ReadStream()
        {
            return valueToken.ToObject<Stream>();
        }

        public bool IsString
        {
            get
            {
                return valueToken.Type == JTokenType.String;
            }
        }

        public string ReadString()
        {
            return valueToken.ToObject<string>();
        }

        public T ReadObject<T>()
        {
            if (valueToken is JObject)
            {
                var jObj = valueToken as Newtonsoft.Json.Linq.JObject;
                var jsonText = jObj.ToString();
                var value = JsonConvert.DeserializeObject<T>(jsonText);
                return value;
            }
            return valueToken.Value<T>();
        }

        public object ReadObject()
        {
            var token = valueToken;
            if (token is JValue)
            {
                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();
                if (token.Type == JTokenType.Bytes)
                    return token.Value<byte[]>();
                if (token.Type == JTokenType.Date)
                    return token.Value<DateTime>();
                if (token.Type == JTokenType.Float)
                    return token.Value<float>();
                if (token.Type == JTokenType.Guid)
                    return token.Value<Guid>();
                if (token.Type == JTokenType.Integer)
                    return token.Value<int>();
                if (token.Type == JTokenType.None)
                    return null;
                if (token.Type == JTokenType.Null)
                    return null;
                if (token.Type == JTokenType.String)
                    return token.Value<string>();
                if (token.Type == JTokenType.Uri)
                    return token.Value<Uri>();
            }
            return token.ToObject<object>();
        }

        public IParseToken[] ReadArray()
        {
            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(
                        token => new JsonTokenParser(token))
                    .ToArray();
            if (valueToken.Type == JTokenType.Null)
                return new IParseToken[] { };
            if (valueToken.Type == JTokenType.Undefined)
                return new IParseToken[] { };

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(childToken => new JsonTokenParser(childToken))
                    .ToArray();

            if (valueToken.Type == JTokenType.Property)
            {
                var property = (valueToken as JProperty);
                return new JsonTokenParser(property.Value).AsArray();
            }

            return valueToken
                .Children()
                .Select(child => new JsonTokenParser(child))
                .ToArray();
        }

        public IDictionary<string, IParseToken> ReadDictionary()
        {
            KeyValuePair<string, IParseToken> ParseToken(JToken token)
            {
                if (token.Type == JTokenType.Property)
                {
                    var propertyToken = (token as JProperty);
                    return new JsonTokenParser(propertyToken.Value)
                        .PairWithKey<string, IParseToken>(propertyToken.Name);
                }
                return (new JsonTokenParser(token))
                    .PairWithKey<string, IParseToken>(token.ToString());
            }

            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(ParseToken)
                    .ToDictionary();
            if (valueToken.Type == JTokenType.Null)
                return new Dictionary<string, IParseToken>();
            if (valueToken.Type == JTokenType.Undefined)
                return new Dictionary<string, IParseToken>();

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();

            return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();
        }
    }
}
