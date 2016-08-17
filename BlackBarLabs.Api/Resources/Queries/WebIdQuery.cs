using BlackBarLabs.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public TResult Parse<TResult>(
            Func<IEnumerable<Guid>, TResult> multiple,
            Func<TResult> empty,
            Func<TResult> unparsable)
        {
            Guid singleGuid;
            if(Guid.TryParse(this.query, out singleGuid))
            {
                return multiple(singleGuid.ToEnumerable());
            }
            return unparsable();
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
