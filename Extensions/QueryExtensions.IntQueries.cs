using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
using System;
using System.ComponentModel;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        [QueryParameterType(WebIdQueryType = typeof(IntEmptyAttribute))]
        public static int? ParamEmpty(this IntQuery query)
        {
            return query.Parse(
                (v) =>
                {
                    if (!(v is IntEmptyAttribute))
                        throw new InvalidOperationException("Do not use ParamEmpty outside of ParseAsync");

                    return default(int?);
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }
        
        [QueryParameterType(WebIdQueryType = typeof(IntValueAttribute))]
        public static int ParamValue(this IntQuery query)
        {
            return query.Parse(
                (v) =>
                {
                    if (!(v is IntValueAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    var wiqo = v as IntValueAttribute;
                    return wiqo.Value;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }
        

        [QueryParameterType(WebIdQueryType = typeof(IntMaybeAttribute), IsOptional = true)]
        public static int? ParamMaybe(this IntQuery query)
        {
            if (query.IsDefault())
                return default(int?);

            return query.Parse(
                (v) =>
                {
                    if (!(v is IntMaybeAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    var wiqo = v as IntMaybeAttribute;
                    return wiqo.ValueMaybe;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        internal static TResult ParseInternal<TResult>(this IntQuery query, string value,
            Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            if (int.TryParse(value, out int specificValue))
                return parsed(new IntValueAttribute(specificValue));

            if (String.Compare("empty", value.ToLower()) == 0)
                return parsed(new IntEmptyAttribute());
            if (String.Compare("null", value.ToLower()) == 0)
                return parsed(new IntEmptyAttribute());

            return unparsable($"Could not parse '{value}' to bool");
        }

        private class IntMaybeAttribute : QueryMatchAttribute
        {
            public int? ValueMaybe;

            public IntMaybeAttribute(int? value)
            {
                this.ValueMaybe = value;
            }
        }

        private class IntValueAttribute : IntMaybeAttribute
        {
            public int Value;

            public IntValueAttribute(int value)
                : base(value)
            {
                this.Value = value;
            }
        }

        private class IntEmptyAttribute : IntMaybeAttribute
        {
            public IntEmptyAttribute() : base(default(int?))
            {

            }
        }
        
    }
}
