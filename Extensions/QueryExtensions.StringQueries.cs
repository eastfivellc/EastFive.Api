using BlackBarLabs.Api.Resources;
using EastFive.Extensions;
using System;
using System.Linq;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {

        [QueryParameterType(WebIdQueryType = typeof(StringMaybeParameterAttribute), IsOptional = true)]
        public static string ParamMaybe(this StringQuery query)
        {
            if (query.IsDefaultOrNull())
                return default(string);
            return query.Parse(
                (v) =>
                {
                    if (!(v is StringMaybeParameterAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    var wiqo = v as StringMaybeParameterAttribute;
                    return wiqo.Value;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        [QueryParameterType(WebIdQueryType = typeof(StringValueParameterAttribute))]
        public static string ParamValue(this StringQuery query)
        {
            return query.Parse(
                (v) =>
                {
                    if (!(v is StringValueParameterAttribute))
                        throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

                    var wiqo = v as StringValueParameterAttribute;
                    return wiqo.Value;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }
        
        class StringValueParameterAttribute : StringMaybeParameterAttribute
        {
            public StringValueParameterAttribute(string value)
                : base(value)
            {
                this.Value = value;
            }
        }

        class StringMaybeParameterAttribute : QueryMatchAttribute
        {
            internal string Value;

            public StringMaybeParameterAttribute(string value)
            {
                this.Value = value;
            }
        }

        internal static TResult ParseInternal<TResult>(this StringQuery query, string value,
            Func<QueryMatchAttribute, TResult> parsed)
        {
            return parsed(new StringValueParameterAttribute(value));
        }
    }
}
