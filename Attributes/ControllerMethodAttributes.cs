using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPostAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpGetAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HttpPutAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HttpDeleteAttribute : Attribute
    {

    }
}
