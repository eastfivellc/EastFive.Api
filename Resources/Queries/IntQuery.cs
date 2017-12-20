using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using BlackBarLabs.Extensions;

namespace BlackBarLabs.Api.Resources
{
    [TypeConverter(typeof(IntQueryConverter))]
    public class IntQuery : IQueryParameter
    {
        private string query;
        
        public static implicit operator IntQuery(string query)
        {
            if(default(string) == query)
                return default(IntQuery);
            return new IntQuery() { query = query };
        }

        public static implicit operator IntQuery(int query)
        {
            return new IntQuery() { query = query.ToString() };
        }

        public TResult Parse<TResult>(Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            return this.ParseInternal(this.query, parsed, unparsable);
        }
    }
    
    class IntQueryConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            if (sourceType == typeof(int))
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
                IntQuery query = valueString;
                return query;
            }
            if (value is int)
            {
                IntQuery query = value.ToString();
                return query;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
