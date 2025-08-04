﻿using EastFive.Api.Resources;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EastFive.Api
{
    public class PropertyOptionalAttribute : PropertyAttribute
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var baseValue = base.TryCast(bindingData);
            if (baseValue.valid)
                return baseValue;

            baseValue.valid = true;
            baseValue.fromBody = true;
            baseValue.value = GetValue();
            return baseValue;

            object GetValue()
            {
                var parameterType = parameterRequiringValidation.ParameterType;
                if (parameterType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var refType = parameterType.GenericTypeArguments.First();
                    var parameterTypeGeneric = RefOptionalHelper.CreateEmpty(refType);
                    return parameterTypeGeneric;
                }

                if (parameterType.IsSubClassOfGeneric(typeof(IRefs<>)))
                {
                    var refType = parameterType.GenericTypeArguments.First();
                    var parameterTypeGeneric = typeof(Refs<>).MakeGenericType(new Type[] { refType });
                    var refIds = new Guid[] { };
                    var refIdsLookupType = typeof(Func<,>).MakeGenericType(new Type[] { typeof(Guid), refType });
                    var refIdsLookup = refIdsLookupType.GetDefault();
                    return Activator.CreateInstance(
                        parameterTypeGeneric, new object[] { refIds });
                }

                if (parameterType.IsSubClassOfGeneric(typeof(IDictionary<,>)) &&
                    parameterType.GenericTypeArguments.AnyNullSafe())
                {
                    var parameterTypeGeneric = typeof(Dictionary<,>).MakeGenericType(parameterType.GenericTypeArguments);
                    return Activator.CreateInstance(parameterTypeGeneric);
                }

                return parameterType.GetDefault();
            }
        }

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Required = false;
            return parameter;
        }
    }
}
