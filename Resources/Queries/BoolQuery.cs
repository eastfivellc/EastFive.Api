using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using BlackBarLabs.Extensions;

namespace BlackBarLabs.Api.Resources
{
    [TypeConverter(typeof(BoolQueryConverter))]
    public class BoolQuery : IQueryParameter
    {
        private string query;
        
        public static implicit operator BoolQuery(string query)
        {
            if(default(string) == query)
                return default(BoolQuery);
            return new BoolQuery() { query = query };
        }

        public static implicit operator BoolQuery(bool query)
        {
            return new BoolQuery() { query = query.ToString() };
        }

        public TResult Parse<TResult>(Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            return this.ParseInternal(this.query, parsed, unparsable);
        }
    }
    
    class BoolQueryConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            if (sourceType == typeof(bool))
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
                BoolQuery query = valueString;
                return query;
            }
            if (value is bool)
            {
                BoolQuery query = value.ToString();
                return query;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
