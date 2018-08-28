using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api.Controllers
{
    public delegate HttpResponseMessage GeneralConflictResponse(string value);
    public delegate HttpResponseMessage GeneralFailureResponse(string value);
    public delegate HttpResponseMessage CreatedResponse();
    public delegate HttpResponseMessage CreatedBodyResponse(object content, string contentType = default(string));
    public delegate HttpResponseMessage AlreadyExistsResponse();
    public delegate HttpResponseMessage AlreadyExistsReferencedResponse(Guid value);
    public delegate HttpResponseMessage NoContentResponse();
    public delegate HttpResponseMessage AcceptedResponse();
    public delegate HttpResponseMessage NotFoundResponse();
    public delegate HttpResponseMessage ContentResponse(object content, string contentType = default(string));
    public delegate HttpResponseMessage ViewFileResponse(string viewPath, object content);
    public delegate HttpResponseMessage ViewStringResponse(string view, object content);
    public delegate Task<HttpResponseMessage> MultipartResponseAsync(IEnumerable<HttpResponseMessage> responses);
    public delegate Task<HttpResponseMessage> MultipartAcceptArrayResponseAsync(IEnumerable<object> responses);

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse();

    /// <summary>
    /// The when creating a document referenced in the create does not exits.
    /// </summary>
    /// <returns></returns>
    public delegate HttpResponseMessage ReferencedDocumentDoesNotExistsResponse();
    public delegate HttpResponseMessage UnauthorizedResponse();
    public delegate HttpResponseMessage NotModifiedResponse();
    public delegate HttpResponseMessage RedirectResponse(Uri redirectLocation);

}
