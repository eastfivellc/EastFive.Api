using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace EastFive.Api
{
    public class JsonConvertGraphicsAttribute : Attribute, IConvertJson
    {
        public bool CanConvert(Type objectType)
        {
            return objectType == typeof(RectangleF);
        }

        public void Write(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var rectF = (RectangleF)value;
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(rectF.X);
            writer.WritePropertyName("y");
            writer.WriteValue(rectF.Y);
            writer.WritePropertyName("width");
            writer.WriteValue(rectF.Width);
            writer.WritePropertyName("height");
            writer.WriteValue(rectF.Height);
            writer.WriteEndObject();
        }
    }
}
