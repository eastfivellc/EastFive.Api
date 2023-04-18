using System;
using System.Linq;

namespace EastFive.Api.Extensions
{
	public static class ReflectionExtensions
	{
        public static bool TryGetAttributeInterfaceFromChain<T>(this System.Reflection.ParameterInfo parameterInfo,
            IApplication application,
            out T attributeInterface,
            bool inherit = false)
        {
            if (!typeof(T).IsInterface)
                throw new ArgumentException($"{typeof(T).FullName} is not an interface.");

            var attributes = parameterInfo.GetAttributesInterface<T>(inherit)
                .Select(attr => (T)attr)
                .ToArray();
            if (attributes.Any())
            {
                attributeInterface = attributes.First();
                return true;
            }

            if (application.GetType().TryGetAttributeInterface(out attributeInterface, inherit: inherit))
                return true;

            if (parameterInfo.ParameterType.TryGetAttributeInterface(out attributeInterface, inherit: inherit))
                return true;

            //attributeInterface = default;
            return false;
        }
    }
}

