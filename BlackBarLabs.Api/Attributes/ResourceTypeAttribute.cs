using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api
{

    [AttributeUsage(AttributeTargets.Class)]
    public class ResourceTypeAttribute : System.Attribute
    {
        public ResourceTypeAttribute()
        {
        }
        
        private string urn;
        public string Urn
        {
            get
            {
                return this.urn;
            }
            set
            {
                urn = value;
            }
        }
        
    }
}
