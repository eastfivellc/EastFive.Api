using BlackBarLabs.Api.Resources;
using System;
using System.ComponentModel;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        [QueryParameterType(WebIdQueryType = typeof(BoolEmptyAttribute))]
        public static bool? ParamEmpty(this BoolQuery query)
        {
            return query.Parse(
                (v) =>
                {
                    if (!(v is BoolValueAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    return default(bool?);
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }
        
        [QueryParameterType(WebIdQueryType = typeof(BoolValueAttribute))]
        public static bool ParamValue(this BoolQuery query)
        {
            return query.Parse(
                (v) =>
                {
                    if (!(v is BoolValueAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    var wiqo = v as BoolValueAttribute;
                    return wiqo.Value;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }
        
        internal static TResult ParseInternal<TResult>(this BoolQuery query, string value,
            Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            bool specificValue;
            if (bool.TryParse(value, out specificValue))
                return parsed(new BoolValueAttribute(specificValue));

            if (String.Compare("empty", value.ToLower()) == 0)
                return parsed(new BoolEmptyAttribute());
            if (String.Compare("null", value.ToLower()) == 0)
                return parsed(new BoolEmptyAttribute());

            return unparsable($"Could not parse '{value}' to bool");
        }

        private class BoolValueAttribute : QueryMatchAttribute
        {
            public bool Value;

            public BoolValueAttribute(bool value)
            {
                this.Value = value;
            }
        }

        private class BoolEmptyAttribute : QueryMatchAttribute
        {
        }
        
    }
}
