using BlackBarLabs.Api.Resources;
using System;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        internal static TResult ParseInternal<TResult>(this DateTimeQuery query,
            Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            return query.ParseInternal(
                (start, end) => parsed(new DateTimeRangeAttribute(start, end)),
                (when) => parsed(new DateTimeValueAttribute(when)),
                () => parsed(new DateTimeAnyAttribute()),
                () => parsed(new DateTimeEmptyAttribute()),
                () => unparsable("Could not parse date time query"));
        }

        #region ParamCalls

        [QueryParameterType(WebIdQueryType = typeof(DateTimeValueAttribute))]
        public static DateTime ParamValue(this DateTimeQuery query)
        {
            return query.Parse(
                (v) =>
                {
                    if (!(v is DateTimeValueAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");
                    var dtValue = v as DateTimeValueAttribute;
                    return dtValue.Value;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        [QueryParameterType(WebIdQueryType = typeof(DateTimeEmptyAttribute))]
        public static DateTime? ParamEmpty(this DateTimeQuery query)
        {
            return query.Parse(
                   (v) =>
                   {
                       if (!(v is DateTimeEmptyAttribute))
                           throw new InvalidOperationException("Do not use ParamEmpty outside of ParseAsync");
                       return default(DateTime?);
                   },
                   (why) =>
                   {
                       throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                   });
        }

        [QueryParameterType(WebIdQueryType = typeof(DateTimeAnyAttribute))]
        public static object ParamAny(this DateTimeQuery query)
        {
            return query.Parse(
                   (v) =>
                   {
                       if (!(v is DateTimeAnyAttribute))
                           throw new InvalidOperationException("Do not use ParamAny outside of ParseAsync");
                       return default(DateTime?);
                   },
                   (why) =>
                   {
                       throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                   });
        }

        public struct DateTimeRange
        {
            public DateTime from;
            public DateTime to;
        }

        [QueryParameterType(WebIdQueryType = typeof(DateTimeRangeAttribute))]
        public static DateTimeRange ParamRange(this DateTimeQuery query)
        {
            return query.Parse(
                   (v) =>
                   {
                       if (!(v is DateTimeRangeAttribute))
                           throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");
                       var dtRange = v as DateTimeRangeAttribute;
                       return new DateTimeRange { from = dtRange.From, to = dtRange.To };
                   },
                   (why) =>
                   {
                       throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                   });
        }

        #endregion

        private class DateTimeValueAttribute : QueryMatchAttribute
        {
            public DateTime Value;

            public DateTimeValueAttribute(DateTime value)
            {
                this.Value = value;
            }
        }

        private class DateTimeRangeAttribute : QueryMatchAttribute
        {
            public DateTime From;
            public DateTime To;

            public DateTimeRangeAttribute(DateTime from, DateTime to)
            {
                this.From = from;
                this.To = to;
            }
        }

        private class DateTimesAttribute : QueryMatchAttribute
        {
            public DateTime [] DateTimesValue;

            public DateTimesAttribute(DateTime [] dateTimes)
            {
                this.DateTimesValue = dateTimes;
            }
        }
        
        private class DateTimeEmptyAttribute : QueryMatchAttribute
        {
        }

        private class DateTimeAnyAttribute : QueryMatchAttribute
        {
        }

    }
}
