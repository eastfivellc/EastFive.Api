using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

using BlackBarLabs.Extensions;

namespace BlackBarLabs.Api.Resources
{
    [TypeConverter(typeof(StringQueryConverter))]
    public class StringQuery : IQueryParameter
    {
        public string UUIDs { get; set; }

        public string URN { get; set; }

        public string Source { get; set; }

        private string query;

        public bool IsSpecified()
        {
            return true;
        }

        public static implicit operator StringQuery(string query)
        {
            return new StringQuery() { query = query };
        }
        
        public TResult Parse<TResult>(
            Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            return this.ParseInternal(query, parsed);
        }
    }

    class StringQueryConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context,
            CultureInfo culture, object value)
        {
            if (value is string)
            {
                var valueString = value as string;
                StringQuery query = valueString;
                return query;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
