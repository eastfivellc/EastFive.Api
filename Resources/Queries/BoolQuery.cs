using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using BlackBarLabs.Extensions;

namespace BlackBarLabs.Api.Resources
{
    [TypeConverter(typeof(DateTimeQueryConverter))]
    public class BoolQuery : TypeConverter, IWebParsable
    {
        private string query;

        public bool IsSpecified()
        {
            return !String.IsNullOrWhiteSpace(query);
        }

        public static implicit operator BoolQuery(string query)
        {
            if(default(string) == query)
                return default(BoolQuery);
            return new BoolQuery() { query = query };
        }

        internal TResult ParseInternal<TResult>(
            Func<bool?, TResult> specific,
            Func<TResult> unparsable)
        {
            bool specificValue;
            if (bool.TryParse(query, out specificValue))
            {
                return specific(specificValue);
            }

            if (String.Compare("empty", this.query.ToLower()) == 0)
                return specific(default(bool?));
            if (String.Compare("null", this.query.ToLower()) == 0)
                return specific(default(bool?));

            return unparsable();
        }
    }

    public static class BoolQueryExtensions
    {
        public static TResult Parse<TResult>(this BoolQuery query,
            Func<bool?, TResult> specific,
            Func<TResult> unspecified,
            Func<TResult> unparsable)
        {
            return query.HasValue(
                (queryNotNull) => queryNotNull.ParseInternal(specific, unparsable),
                () => unspecified());
        }
    }

    class BoolQueryConverter : TypeConverter
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
                BoolQuery query = valueString;
                return query;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
