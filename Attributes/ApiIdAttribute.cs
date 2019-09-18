using BlackBarLabs.Extensions;
using EastFive.Extensions;
using EastFive.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ApiIdAttribute : System.Attribute, IProvideApiValue
    {
        public ApiIdAttribute()
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
