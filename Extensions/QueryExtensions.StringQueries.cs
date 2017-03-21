using BlackBarLabs.Api.Resources;
using System;
using System.Linq;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
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
        
        class StringValueParameterAttribute : QueryMatchAttribute
        {
            internal string Value;

            public StringValueParameterAttribute(string value)
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
