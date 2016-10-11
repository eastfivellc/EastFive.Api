using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api
{
    public static class TypeExtensions
    {
        public static string AsJsonType(this Type type)
        {
            if (typeof(int) == type)
                return "integer";
            if (typeof(long) == type)
                return "integer";
            if (typeof(double) == type)
                return "number";
            if (typeof(float) == type)
                return "number";
            if (typeof(decimal) == type)
                return "number";
            if (typeof(bool) == type)
                return "boolean";
            if (typeof(string) == type)
                return "string";

            if (type.IsArray)
                return "array";

            return "object";
        }
    }
}
