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
    public delegate HttpResponseMessage AlreadyExistsResponse();
    public delegate HttpResponseMessage AlreadyExistsReferencedResponse(Guid value);
    public delegate HttpResponseMessage NoContentResponse();
    public delegate HttpResponseMessage AcceptedResponse();
    public delegate HttpResponseMessage NotFoundResponse();
    public delegate HttpResponseMessage ContentResponse(object content);
    public delegate Task<HttpResponseMessage> MultipartResponseAsync(IEnumerable<HttpResponseMessage> responses);
    public delegate Task<HttpResponseMessage> MultipartAcceptResponseAsync(IEnumerable<object> responses);
    public delegate Task<HttpResponseMessage> MultipartAcceptArrayResponseAsync(IEnumerable<object> responses);
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse();
    public delegate HttpResponseMessage UnauthorizedResponse();
    public delegate HttpResponseMessage NotModifiedResponse();
    
}
