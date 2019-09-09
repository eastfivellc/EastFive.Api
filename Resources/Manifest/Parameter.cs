using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Resources
{
    public interface IDocumentParameter
    {
        Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp);
    }

    public class Parameter
    {
        public Parameter()
        {
        }

        public static string GetTypeName(Type type, HttpApplication httpApp)
        {
            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                return $"->{GetTypeName(type.GenericTypeArguments.First(), httpApp)}";
            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
                return $"[]->{GetTypeName(type.GenericTypeArguments.First(), httpApp)}";
            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return $"->{GetTypeName(type.GenericTypeArguments.First(), httpApp)}?";
            if (type.IsArray)
                return $"{GetTypeName(type.GetElementType(), httpApp)}[]";
            return type.IsNullable(
                nullableBase => $"{GetTypeName(nullableBase, httpApp)}?",
                () => httpApp.GetResourceName(type));
        }

        public string Name { get; set; }
        public bool Required { get; set; }
        public bool Default { get; set; }
        public string Where { get; set; }
        public string Type { get; set; }
    }
}
