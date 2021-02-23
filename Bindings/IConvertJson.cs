using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EastFive.Api
{
    public interface IConvertJson
    {
        bool CanConvert(Type objectType);
        void Write(JsonWriter writer, object value, JsonSerializer serializer);
    }
}
