using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
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
                    if (!(v is BoolEmptyAttribute))
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

        [QueryParameterType(WebIdQueryType = typeof(BoolMaybeAttribute), IsOptional = true)]
        public static bool? ParamMaybe(this BoolQuery query)
        {
            if (query.IsDefault())
                return default(bool?);

            return query.Parse(
                (v) =>
                {
                    if (!(v is BoolMaybeAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    var wiqo = v as BoolMaybeAttribute;
                    return wiqo.ValueMaybe;
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
            if (bool.TryParse(value, out bool specificValue))
                return parsed(new BoolValueAttribute(specificValue));

            if (String.Compare("empty", value.ToLower()) == 0)
                return parsed(new BoolEmptyAttribute());
            if (String.Compare("null", value.ToLower()) == 0)
                return parsed(new BoolEmptyAttribute());

            return unparsable($"Could not parse '{value}' to bool");
        }

        private class BoolMaybeAttribute : QueryMatchAttribute
        {
            public bool? ValueMaybe;

            public BoolMaybeAttribute(bool? value)
            {
                this.ValueMaybe = value;
            }
        }

        private class BoolValueAttribute : BoolMaybeAttribute
        {
            public bool Value;

            public BoolValueAttribute(bool value)
                : base(value)
            {
                this.Value = value;
            }
        }

        private class BoolEmptyAttribute : BoolMaybeAttribute
        {
            public BoolEmptyAttribute() : base(default(bool?))
            {

            }
        }

    }
}
