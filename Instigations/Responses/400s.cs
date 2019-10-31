using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Linq.Expressions;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Api
{
    [BadRequestGenericResponse]
    public delegate HttpResponseMessage BadRequestResponse<TQuery>(
        Expression<Func<TQuery, bool>> problem);
    public class BadRequestGenericResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;

        [InstigateMethod]
        public HttpResponseMessage BadRequestResponse<TQuery>(Expression<Func<TQuery, bool>> problem)
        {
            Type GetType(Type type)
            {
                if (type.IsArray)
                    return GetType(type.GetElementType());
                return type;
            }

            return request.CreateResponse(this.StatusCode);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.BadRequest)]
    public delegate HttpResponseMessage BadRequestResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.Unauthorized)]
    public delegate HttpResponseMessage UnauthorizedResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.Forbidden)]
    public delegate HttpResponseMessage ForbiddenResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.NotFound)]
    public delegate HttpResponseMessage NotFoundResponse();

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [StatusCodeResponse(StatusCode = HttpStatusCode.NotFound)]
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse();

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [ReferencedDocumentNotFoundResponse]
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse<TResource>();
    public class ReferencedDocumentNotFoundResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;

        [InstigateMethod]
        public HttpResponseMessage ReferencedDocumentNotFound<TResource>()
        {
            return request.CreateResponse(this.StatusCode);
        }
    }

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [ReferencedDocumentNotFoundGenericResponse]
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse<TQuery, TParameter>(
        Expression<Func<TQuery, TParameter>> problem);
    public class ReferencedDocumentNotFoundGenericResponse : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;

        [InstigateMethod]
        public HttpResponseMessage ReferencedDocumentNotFoundResponse<TQuery, TParameter>(
            Expression<Func<TQuery, TParameter>> problem)
        {
            return problem.MemberInfo(
                (member) =>
                {
                    return request
                        .CreateResponse(this.StatusCode)
                        .AddReason($"There are no resources of type `{member.GetMemberType().FullName}` were found.");
                },
                () =>
                {
                    return request
                        .CreateResponse(this.StatusCode)
                        .AddReason($"Inform server developer that {problem} is not a member expression.");
                });
        }
    }

    /// <summary>
    /// When creating or updating a resource, a referenced to a different resource was not found.
    /// </summary>
    /// <returns></returns>
    [ReferencedDocumentDoesNotExistResponse]
    public delegate HttpResponseMessage ReferencedDocumentDoesNotExistsResponse<TResource>();
    public class ReferencedDocumentDoesNotExistResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;

        [InstigateMethod]
        public HttpResponseMessage ReferencedDocumentDoesNotExist<TResource>()
        {
            return request.CreateResponse(this.StatusCode);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.Conflict)]
    public delegate HttpResponseMessage AlreadyExistsResponse();

    [AlreadyExistsReferencedResponse]
    public delegate HttpResponseMessage AlreadyExistsReferencedResponse(Guid value);
    public class AlreadyExistsReferencedResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            AlreadyExistsReferencedResponse dele =
                (existingId) =>
                {
                    return request
                        .CreateResponse(StatusCode)
                        .AddReason($"There is already a resource with ID = [{existingId}]");
                };
            return onSuccess((object)dele);
        }
    }

    [GeneralConflictResponse]
    public delegate HttpResponseMessage GeneralConflictResponse(string value);
    public class GeneralConflictResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            GeneralConflictResponse responseDelegate =
                (why) =>
                {
                    var response = request.CreateResponse(StatusCode);
                    if (why.IsDefaultNullOrEmpty())
                        return response;
                    return response.AddReason(why);
                };
            return onSuccess(responseDelegate);
        }
    }
}
