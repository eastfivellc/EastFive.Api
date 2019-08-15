﻿using EastFive.Linq.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api.Controllers
{
    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.OK)]
    public delegate HttpResponseMessage ContentTypeResponse<TResource>(object content, string contentType = default(string));
    public delegate HttpResponseMessage ContentResponse(object content, string contentType = default(string));
    public delegate HttpResponseMessage HtmlResponse(string content);
    public delegate HttpResponseMessage XlsxResponse(byte [] content, string name);
    public delegate HttpResponseMessage PdfResponse(byte[] content, string name, bool inline);

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedResponse();

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.NoContent)]
    public delegate HttpResponseMessage NoContentResponse();
    public delegate HttpResponseMessage NotModifiedResponse();
    public delegate HttpResponseMessage AcceptedResponse();
    public delegate HttpResponseMessage AcceptedBodyResponse(object content, string contentType = default(string));

    [HttpActionDelegate(StatusCode = System.Net.HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedBodyResponse<TResource>(object content, string contentType = default(string));
    public delegate HttpResponseMessage RedirectResponse(Uri redirectLocation);
    public delegate Task<HttpResponseMessage> MultipartResponseAsync(IEnumerable<HttpResponseMessage> responses);
    public delegate Task<HttpResponseMessage> MultipartAcceptArrayResponseAsync(IEnumerable<object> responses);
    public delegate Task<HttpResponseMessage> MultipartResponseAsync<TResource>(IEnumerableAsync<TResource> responses);

    public delegate HttpResponseMessage ViewFileResponse(string viewPath, object content);
    public delegate HttpResponseMessage ViewStringResponse(string view, object content);
    public delegate string ViewRenderer(string filePath, object content);
    public delegate string ViewPathResolver(string view);

    public delegate HttpResponseMessage BadRequestResponse();
    public delegate HttpResponseMessage NotFoundResponse();
    public delegate HttpResponseMessage AlreadyExistsResponse();
    public delegate HttpResponseMessage AlreadyExistsReferencedResponse(Guid value);
    public delegate HttpResponseMessage ForbiddenResponse();
    public delegate HttpResponseMessage GeneralConflictResponse(string value);

    public delegate HttpResponseMessage GeneralFailureResponse(string value);
    public delegate HttpResponseMessage ServiceUnavailableResponse();
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

    [Obsolete("Use ExecuteBackgroundResponseAsync instead.")]
    public delegate Task<HttpResponseMessage> BackgroundResponseAsync(Func<Action<double>, Task<HttpResponseMessage>> callback);
}
