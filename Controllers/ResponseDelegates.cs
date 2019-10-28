using EastFive.Linq.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api.Controllers
{
    

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedResponse();

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.NoContent)]
    public delegate HttpResponseMessage NoContentResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.NotModified)]
    public delegate HttpResponseMessage NotModifiedResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Accepted)]
    public delegate HttpResponseMessage AcceptedResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Accepted, Example = "serialized object")]
    public delegate HttpResponseMessage AcceptedBodyResponse(object content, string contentType = default(string));

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Created, Example = "serialized object")]
    public delegate HttpResponseMessage CreatedBodyResponse<TResource>(object content, string contentType = default(string));
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Redirect)]
    public delegate HttpResponseMessage RedirectResponse(Uri redirectLocation);

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.BadRequest)]
    public delegate HttpResponseMessage BadRequestResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.NotFound)]
    public delegate HttpResponseMessage NotFoundResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Conflict)]
    public delegate HttpResponseMessage AlreadyExistsResponse();
    [HttpHeaderDelegate(StatusCode = System.Net.HttpStatusCode.Conflict, HeaderName = "", HeaderValue = "")]
    public delegate HttpResponseMessage AlreadyExistsReferencedResponse(Guid value);
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Conflict)]
    public delegate HttpResponseMessage ForbiddenResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Conflict)]
    public delegate HttpResponseMessage GeneralConflictResponse(string value);

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.InternalServerError)]
    public delegate HttpResponseMessage GeneralFailureResponse(string value);
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.ServiceUnavailable)]
    public delegate HttpResponseMessage ServiceUnavailableResponse();
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.ServiceUnavailable)]
    public delegate HttpResponseMessage ConfigurationFailureResponse(string configurationValue, string message);

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [Obsolete("Please specify the type using the generic version ReferencedDocumentNotFoundResponse<TResource>.")]
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse();
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse<TResource>();

    /// <summary>
    /// When creating or updating a resource, a referenced to a different resource was not found.
    /// </summary>
    /// <returns></returns>
    public delegate HttpResponseMessage ReferencedDocumentDoesNotExistsResponse<TResource>();
    public delegate HttpResponseMessage UnauthorizedResponse();

    public delegate HttpResponseMessage NotImplementedResponse();

    public interface IExecuteAsync
    {
        bool ForceBackground { get; }

        Task<HttpResponseMessage> InvokeAsync(Action<double> updateCallback);
    }

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Accepted)]
    public delegate Task<HttpResponseMessage> ExecuteBackgroundResponseAsync(IExecuteAsync executeAsync);

    //[Obsolete("Use ExecuteBackgroundResponseAsync instead.")]
    //public delegate Task<HttpResponseMessage> BackgroundResponseAsync(Func<Action<double>, Task<HttpResponseMessage>> callback);
}
