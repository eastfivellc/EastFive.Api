using System;
using System.ComponentModel;
using System.Globalization;

namespace BlackBarLabs.Api.Resources
{
    [TypeConverter(typeof(DateTimeQueryConverter))]
    public class DateTimeQuery : TypeConverter, IWebParsable
    {
        private string query;

        public bool IsSpecified()
        {
            return true;
        }

        public static implicit operator DateTimeQuery(string query)
        {
            if(default(string) == query)
                return default(DateTimeQuery);
            return new DateTimeQuery() { query = query };
        }

        internal TResult ParseInternal<TResult>(
            Func<DateTime, DateTime, TResult> range,
            Func<DateTime, TResult> specific,
            Func<TResult> unparsable)
        {
            DateTime specificValue;
            if (DateTime.TryParse(query, CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out specificValue))
            {
                return specific(specificValue);
            }

            DateTime start, end;
            int offset = 1;
            int offsetToggle = 1;
            int index = query.Length / 2;
            while (index > 0 && index < query.Length - 1)
            {
                var part1 = query.Substring(0, index);
                var part2 = query.Substring(index);
                if (DateTime.TryParse(part1, out start))
                {
                    if (DateTime.TryParse(part2, CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out end))
                        return range(start, end);

                    // Maybe there is a range character
                    var separatorLength = 1;
                    while (index + separatorLength < query.Length - 1)
                    {
                        part2 = query.Substring(index + separatorLength);
                        if (DateTime.TryParse(part2, CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out end))
                            return range(start, end);
                        separatorLength++;
                    }
                }
                offset += 1;
                index += (offset * offsetToggle);
                offsetToggle *= -1;
            }
            return unparsable();
        }
    }

    public static class DateTimeQueryExtensions
    {
        public static TResult Parse<TResult>(this DateTimeQuery query,
            Func<DateTime, DateTime, TResult> range,
            Func<DateTime, TResult> specific,
            Func<TResult> empty,
            Func<TResult> unspecified,
            Func<TResult> unparsable)
        {
            return query.HasValue(
                (queryNotNull) => queryNotNull.ParseInternal(range, specific, unparsable),
                () => empty());
        }
    }

    class DateTimeQueryConverter : TypeConverter
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
                DateTimeQuery query = valueString;
                return query;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
