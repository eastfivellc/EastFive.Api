using EastFive.Api.Core;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api.Controllers
{
    [ApiSecurity]
    public struct ApiSecurity
    {
        public string key;
    }

    public class ApiSecurityAttribute : Attribute, IInstigatable, IBindApiParameter<string>
    {
        public TResult Bind<TResult>(Type type, string content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return onParsed(new Controllers.ApiSecurity 
            { 
                key = content,
            });
        }

        public Task<IHttpResponse> Instigate(IApplication httpApp,
                IHttpRequest request,
                ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            return EastFive.Web.Configuration.Settings.GetString(AppSettings.ApiKey,
                (authorizedApiKey) =>
                {
                    var queryParams = request.GetAbsoluteUri().ParseQueryString();
                    if (queryParams["ApiKeySecurity"] == authorizedApiKey)
                        return onSuccess(new Controllers.ApiSecurity
                        { 
                            key = queryParams["ApiKeySecurity"],
                        });

                    var authorization = request.GetAuthorization();
                    if (authorization == authorizedApiKey)
                        return onSuccess(new Controllers.ApiSecurity 
                        { 
                            key = authorization,
                        });

                    return request.CreateResponse(HttpStatusCode.Unauthorized).AsTask();
                },
                (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());
        }
    }
}
