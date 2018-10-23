using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ApiPropertyAttribute : System.Attribute
    {
        public ApiPropertyAttribute()
        {
        }
        
        private string propertyName;
        public string PropertyName
        {
            get
            {
                return this.propertyName;
            }
            set
            {
                propertyName = value;
            }
        }
    }
}
