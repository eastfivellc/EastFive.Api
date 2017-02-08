using BlackBarLabs.Api.Resources;
using System;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        private class BoolValue : BoolQuery
        {
            public bool Value;

            public BoolValue(bool value)
            {
                this.Value = value;
            }
        }

        private class BoolEmpty : BoolQuery
        {
        }

        private class BoolBadRequest : BoolQuery
        {
        }

        [QueryParameterType(WebIdQueryType = typeof(BoolValue))]
        public static bool ParamValue(this BoolQuery query)
        {
            if (!(query is BoolValue))
                throw new InvalidOperationException("Do not use ParamValue outside of ParseAsync");

            var dtValue = query as BoolValue;
            return dtValue.Value;
        }

        [QueryParameterType(WebIdQueryType = typeof(BoolEmpty))]
        public static bool? ParamEmpty(this BoolQuery query)
        {
            if (!(query is BoolEmpty))
                throw new InvalidOperationException("Do not use ParamEmpty outside of ParseAsync");
            return default(bool?);
        }
    }
}
