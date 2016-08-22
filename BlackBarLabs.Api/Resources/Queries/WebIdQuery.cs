﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

using BlackBarLabs.Web;

namespace BlackBarLabs.Api.Resources
{
    [TypeConverter(typeof(WebIdQueryConverter))]
    public class WebIdQuery
    {
        private string query;

        public static implicit operator WebIdQuery(string query)
        {
            return new WebIdQuery() { query = query };
        }

        public static implicit operator WebIdQuery(Guid value)
        {
            return new WebIdQuery() { query = value.ToString() };
        }

        public static implicit operator WebIdQuery(WebId value)
        {
            return new WebIdQuery() { query = value.UUID.ToString() };
        }

        public TResult Parse<TResult>(
            Func<IEnumerable<Guid>, TResult> multiple,
            Func<TResult> empty,
            Func<TResult> unparsable)
        {
            if (String.IsNullOrWhiteSpace(this.query))
                return empty();

            Guid singleGuid;
            if(Guid.TryParse(this.query, out singleGuid))
                return multiple(singleGuid.ToEnumerable());
            
            var guidRegex = @"([a-f0-9A-F]{32}|([a-f0-9A-F]{8}-[a-f0-9A-F]{4}-[a-f0-9A-F]{4}-[a-f0-9A-F]{4}-[a-f0-9A-F]{12}))";
            if(!Regex.IsMatch(this.query, guidRegex))
                return unparsable();

            var matches = Regex.Matches(this.query, guidRegex);
            var ids = RegexToEnumerable(matches);
            return multiple(ids);
        }

        private static IEnumerable<Guid> RegexToEnumerable(MatchCollection matches)
        {
            foreach (Match match in matches)
            {
                yield return Guid.Parse(match.Value);
            }
        }

        public TResult Parse<TResult>(
            HttpRequestMessage request,
            Func<Guid, TResult> single,
            Func<IEnumerable<Guid>, TResult> multiple,
            Func<TResult> empty,
            Func<TResult> unparsable)
        {
            if (String.IsNullOrWhiteSpace(request.RequestUri.Query))
            {
                if (String.IsNullOrWhiteSpace(this.query))
                    return empty();
                Guid singleGuid;
                if (Guid.TryParse(this.query, out singleGuid))
                {
                    return single(singleGuid);
                }
                return unparsable();
            }
            return Parse(multiple, empty, unparsable);
        }
    }

    class WebIdQueryConverter : TypeConverter
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
                WebIdQuery query = valueString;
                return query;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
