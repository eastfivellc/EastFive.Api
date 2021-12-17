using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive;
using EastFive.Api.Meta.Flows;
using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Linq;

namespace EastFive.Api.Auth
{
    public class AuthorizationTokenAttribute
        : System.Attribute, IDefineHeader
    {
        public Header GetHeader(Api.Resources.Method method, ParameterInfo parameter)
        {
            if (!method.MethodPoco.TryGetAttributeInterface(out IValidateHttpRequest requestValidator))
                return new Header()
                {
                    key = "{{AuthorizationHeaderName}}",
                    value = "{{TOKEN}}",
                    type = "text",
                };

            return new Header()
            {
                key = $"api-voucher",
                value = "{{ApiVoucher}}",
                type = "text",
            };
        }
    }
}
