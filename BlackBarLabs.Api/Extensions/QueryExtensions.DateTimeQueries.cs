using BlackBarLabs.Api.Resources;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Core.Collections;
using BlackBarLabs.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        private class DateTimeValue : DateTimeQuery
        {
            public DateTime Value;

            public DateTimeValue(DateTime value)
            {
                this.Value = value;
            }
        }

        private class DateTimeRangeQuery : DateTimeQuery
        {
            public DateTime From;
            public DateTime To;

            public DateTimeRangeQuery(DateTime from, DateTime to)
            {
                this.From = from;
                this.To = to;
            }
        }

        private class DateTimes : DateTimeQuery
        {
            public DateTime [] DateTimesValue;

            public DateTimes(DateTime [] dateTimes)
            {
                this.DateTimesValue = dateTimes;
            }
        }


        private class DateTimeEmpty : DateTimeQuery
        {
        }

        private class DateTimeBadRequest : DateTimeQuery
        {
        }

        [QueryParameterType(WebIdQueryType = typeof(DateTimeValue))]
        public static DateTime ParamValue(this DateTimeQuery query)
        {
            if (!(query is DateTimeValue))
                throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

            var dtValue = query as DateTimeValue;
            return dtValue.Value;
        }

        [QueryParameterType(WebIdQueryType = typeof(DateTimeEmpty))]
        public static DateTime? ParamEmpty(this DateTimeQuery query)
        {
            if (!(query is DateTimeEmpty))
                throw new InvalidOperationException("Do not use ParamEmpty outside of ParseAsync");
            return default(DateTime?);
        }

        public struct DateTimeRange
        {
            public DateTime from;
            public DateTime to;
        }

        [QueryParameterType(WebIdQueryType = typeof(DateTimeRange))]
        public static DateTimeRange ParamRange(this DateTimeQuery query)
        {
            if (!(query is DateTimeRangeQuery))
                throw new InvalidOperationException("Do not use ParamOr outside of ParseAsync");

            var dtRange = query as DateTimeRangeQuery;
            return new DateTimeRange { from = dtRange.From, to = dtRange.To };
        }
    }
}
