using BlackBarLabs.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api.Controllers
{
    public static class ApiValidations
    {
        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Delegate)]
        public class ValidationAttribute : Attribute
        {
        }

        public class ValidationValueAttribute : ValidationAttribute
        {

        }

        public class ValidationDefaultAttribute : ValidationAttribute
        {

        }

        public class ValidationMultipleAttribute : ValidationAttribute
        {

        }

        public class ValidationUnspecified : ValidationAttribute
        {

        }

        public class ValidationAnyAttribute : ValidationAttribute
        {

        }

        public class ValidationInvalidAttribute : ValidationAttribute
        {

        }

        public class ValidationRangeAttribute : ValidationAttribute
        {

        }

        [ValidationValue]
        public static Guid ParamGuid(this WebId sourceValue)
        {
            return sourceValue.UUID;
        }

        [ValidationValue]
        public static Guid ParamGuid(this WebIdQuery sourceValue, HttpRequestMessage request)
        {
            return sourceValue.Parse2(request,
                (v) => v,
                (v) => { throw new Exception("ParamGuid for WebIDQuery matched multiple."); },
                () => { throw new Exception("ParamGuid for WebIDQuery matched unspecified."); },
                () => { throw new Exception("ParamGuid for WebIDQuery matched unparsable."); });
        }

        [ValidationAny]
        public static bool ParamDatetimeAny(this BlackBarLabs.Api.Resources.DateTimeQuery sourceValue)
        {

            return sourceValue.ParseInternal(
                (v1, v2) => { throw new Exception("ParamDatetimeAny for DateTimeQuery matched range."); },
                (v) => true,
                () => true,
                () => { throw new Exception("ParamDatetimeAny for DateTimeQuery matched empty."); },
                () => { throw new Exception("ParamDatetimeAny for DateTimeQuery matched invalid value."); });
        }

        [ValidationUnspecified]
        public static bool ParamDatetimeEmpty(this BlackBarLabs.Api.Resources.DateTimeQuery sourceValue)
        {
            return sourceValue.ParseInternal(
                (v1, v2) => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched range."); },
                (v) => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched value."); },
                () => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched any."); },
                () => false,
                () => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched invalid value."); });
        }
    }
}
