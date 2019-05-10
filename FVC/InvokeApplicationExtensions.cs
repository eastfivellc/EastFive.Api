using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using EastFive.Linq;
using EastFive.Collections.Generic;
using EastFive;
using EastFive.Reflection;
using EastFive.Extensions;
using EastFive.Api.Controllers;
using EastFive.Linq.Expressions;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api;
using EastFive.Linq.Async;

namespace EastFive.Api
{
    public static class InvokeApplicationExtensions
    {
        public static Uri Location<T>(this IQueryable<T> urlQuery)
        {
            if (urlQuery is IRenderUrls)
                return (urlQuery as IRenderUrls).RenderLocation();

            var values = urlQuery.ToString();
            return new Uri(values);
        }

        public static Task<TResult> MethodAsync<TResource, TResult>(this RequestMessage<TResource> request,
                HttpMethod method)
        {
            return typeof(TResource).GetCustomAttribute<FunctionViewControllerAttribute, Task<TResult>>(
                async fvcAttr =>
                {
                    var httpRequest = request.Request;
                    httpRequest.Method = method;
                    var response = await request.SendAsync(httpRequest);

                    if (response is IDidNotOverride)
                        (response as IDidNotOverride).OnFailure();
                    if (!(response is IReturnResult))
                        throw new Exception($"Failed to override response with status code `{response.StatusCode}` for {typeof(TResource).FullName}\nResponse:{response.ReasonPhrase}");

                    var attachedResponse = response as IReturnResult;
                    var result = attachedResponse.GetResultCasted<TResult>();
                    return result;
                },
                () =>
                {
                    throw new Exception($"Type {typeof(TResource).FullName} does not have FunctionViewControllerAttribute");
                });
        }

        public static Task<TResult> MethodAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
                HttpMethod method,

            Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<object[], TResult> onContentObjects = default(Func<object[], TResult>),
            Func<string, TResult> onHtml = default(Func<string, TResult>),
            Func<byte[], string, TResult> onXls = default(Func<byte[], string, TResult>),
            Func<TResult> onCreated = default(Func<TResult>),
            Func<TResource, string, TResult> onCreatedBody = default(Func<TResource, string, TResult>),
            Func<TResult> onUpdated = default(Func<TResult>),

            Func<Uri, string, TResult> onRedirect = default(Func<Uri, string, TResult>),

            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onUnauthorized = default(Func<TResult>),
            Func<TResult> onExists = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<string, TResult> onFailure = default(Func<string, TResult>),

            Func<TResult> onNotImplemented = default(Func<TResult>),
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default(Func<IExecuteAsync, Task<TResult>>))
        {
            var request = (requestQuery as RequestMessage<TResource>);
            var application = request.Application;
            application.CreatedResponse<TResource, TResult>(onCreated);
            application.CreatedBodyResponse<TResource, TResult>(onCreatedBody);
            application.BadRequestResponse<TResource, TResult>(onBadRequest);
            application.AlreadyExistsResponse<TResource, TResult>(onExists);
            application.RefNotFoundTypeResponse(onRefDoesNotExistsType);
            application.RedirectResponse<TResource, TResult>(onRedirect);
            application.NotImplementedResponse<TResource, TResult>(onNotImplemented);

            application.ContentResponse(onContent);
            application.ContentTypeResponse<TResource, TResult>((body, contentType) => onContent(body));
            application.MultipartContentResponse(onContents);
            if(!onContentObjects.IsDefaultOrNull())
                application.MultipartContentObjectResponse<TResource, TResult>(onContentObjects);
            application.NotFoundResponse<TResource, TResult>(onNotFound);
            application.HtmlResponse<TResource, TResult>(onHtml);
            application.XlsResponse<TResource, TResult>(onXls);

            application.NoContentResponse<TResource, TResult>(onUpdated);
            application.UnauthorizedResponse<TResource, TResult>(onUnauthorized);
            application.GeneralConflictResponse<TResource, TResult>(onFailure);
            application.ExecuteBackgroundResponse<TResource, TResult>(onExecuteBackground);

            return request.MethodAsync<TResource, TResult>(method);
        }

        public static Task<TResult> GetAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
            Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<object[], TResult> onContentObjects = default(Func<object[], TResult>),
            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<Uri, string, TResult> onRedirect = default(Func<Uri, string, TResult>),
            Func<TResult> onCreated = default(Func<TResult>),
            Func<string, TResult> onHtml = default(Func<string, TResult>),
            Func<byte[], string, TResult> onXls = default(Func<byte[], string, TResult>),
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default(Func<IExecuteAsync, Task<TResult>>))
        {
            return requestQuery.MethodAsync<TResource, TResult>(HttpMethod.Get,
                onContent: onContent,
                onContents: onContents,
                onContentObjects: onContentObjects,
                onBadRequest: onBadRequest,
                onNotFound: onNotFound,
                onRefDoesNotExistsType: onRefDoesNotExistsType,
                onRedirect: onRedirect,
                onCreated: onCreated,
                onHtml: onHtml,
                onXls: onXls,
                onExecuteBackground: onExecuteBackground);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="application"></param>
        /// <param name="resource"></param>
        /// <param name="onCreated"></param>
        /// <param name="onBadRequest"></param>
        /// <param name="onExists"></param>
        /// <param name="onRefDoesNotExistsType"></param>
        /// <param name="onNotImplemented"></param>
        /// <returns></returns>
        /// <remarks>Response hooks are only called if the method is actually invoked. Responses from the framework are not trapped.</remarks>
        public static Task<TResult> PostAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
                TResource resource,
            Func<TResult> onCreated = default(Func<TResult>),
            Func<TResource, string, TResult> onCreatedBody = default(Func<TResource, string, TResult>),
            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onExists = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<Uri, string, TResult> onRedirect = default(Func<Uri, string, TResult>),
            Func<TResult> onNotImplemented = default(Func<TResult>),
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default(Func<IExecuteAsync, Task<TResult>>))
        {
            return requestQuery.MethodAsync<TResource, TResult>(HttpMethod.Post,
                onCreated: onCreated,
                onCreatedBody: onCreatedBody,
                onBadRequest: onBadRequest,
                onExists: onExists,
                onRefDoesNotExistsType: onRefDoesNotExistsType,
                onRedirect: onRedirect,
                onNotImplemented: onNotImplemented,
                onExecuteBackground: onExecuteBackground);
        }

        public static Task<TResult> PatchAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
                TResource resource,
            Func<TResult> onUpdated = default(Func<TResult>),
            Func<TResource, TResult> onUpdatedBody = default(Func<TResource, TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<TResult> onUnauthorized = default(Func<TResult>),
            Func<string, TResult> onFailure = default(Func<string, TResult>))
        {
            return requestQuery.MethodAsync<TResource, TResult>(new HttpMethod("patch"),
                onUpdated: onUpdated,
                onContent: onUpdatedBody,
                onNotFound: onNotFound,
                onUnauthorized: onUnauthorized,
                onFailure: onFailure);
        }

        private static Task<TResult> DeleteAsync<TResource, TResult>(this RequestMessage<TResource> request,
            Func<TResult> onNoContent = default(Func<TResult>),
            Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<Uri, string, TResult> onRedirect = default(Func<Uri, string, TResult>),
            Func<string, TResult> onHtml = default(Func<string, TResult>))
        {
            return request.MethodAsync<TResource, TResult>(HttpMethod.Delete,
                onUpdated: onNoContent,
                onContent: onContent,
                onContents: onContents,
                onBadRequest: onBadRequest,
                onNotFound: onNotFound,
                onRefDoesNotExistsType: onRefDoesNotExistsType,
                onRedirect: onRedirect,
                onHtml: onHtml);
        }

        
        #region Response types

        public interface IReturnResult
        {
            TResult GetResultCasted<TResult>();
        }

        private class AttachedHttpResponseMessage<TResult> : HttpResponseMessage, IReturnResult
        {
            public TResult Result { get; }

            public AttachedHttpResponseMessage(TResult result)
            {
                this.Result = result;
            }

            public HttpResponseMessage Inner { get; }
            public AttachedHttpResponseMessage(TResult result, HttpResponseMessage inner)
            {
                this.Result = result;
                this.Inner = inner;
            }

            public TResult1 GetResultCasted<TResult1>()
            {
                return (TResult1)(this.Result as object);
            }
        }

        private interface IDidNotOverride
        {
            void OnFailure();
        }

        private class NoOverrideHttpResponseMessage<TResource> : HttpResponseMessage, IDidNotOverride
        {
            private Type typeOfResponse;
            private HttpRequestMessage request;
            public NoOverrideHttpResponseMessage(Type typeOfResponse, HttpRequestMessage request)
            {
                this.typeOfResponse = typeOfResponse;
                this.request = request;
            }

            public void OnFailure()
            {
                var message = $"Failed to override response for: [{request.Method.Method}] `{typeof(TResource).FullName}`.`{typeOfResponse.Name}`";
                throw new Exception(message);
            }
        }

        private static void ContentResponse<TResource, TResult>(this IApplication application,
            Func<TResource, TResult> onContent)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.ContentResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.ContentResponse created =
                        (content, mimeType) =>
                        {
                            if (onContent.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(EastFive.Api.Controllers.ContentResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            if (!(content is TResource))
                                throw new Exception($"Could not cast {content.GetType().FullName} to {typeof(TResource).FullName}.");
                            var resource = (TResource)content;
                            var result = onContent(resource);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                },
                onContent.IsDefaultOrNull());
        }

        private static void HtmlResponse<TResource, TResult>(this IApplication application,
            Func<string, TResult> onHtml)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.HtmlResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.HtmlResponse created =
                        (content) =>
                        {
                            if (onHtml.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(EastFive.Api.Controllers.HtmlResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onHtml(content);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        private static void XlsResponse<TResource, TResult>(this IApplication application,
            Func<byte[], string, TResult> onXls)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.XlsxResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.XlsxResponse created =
                        (content, name) =>
                        {
                            if (onXls.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(EastFive.Api.Controllers.XlsxResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onXls(content, name);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        private static void BadRequestResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onBadRequest)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.BadRequestResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.BadRequestResponse badRequest =
                        () =>
                        {
                            if (onBadRequest.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(EastFive.Api.Controllers.BadRequestResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onBadRequest();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(badRequest);
                });
        }
        
        private static void RefNotFoundTypeResponse<TResult>(this IApplication application,
            Func<Type, TResult> referencedDocDoesNotExists)
        {
            application.SetInstigatorGeneric(
                typeof(ReferencedDocumentDoesNotExistsResponse<>),
                (type, thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    var scope = new CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>(referencedDocDoesNotExists,
                        thisAgain, requestAgain, paramInfo, onSuccess);
                    var multipartResponseMethodInfoGeneric = typeof(CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>)
                        .GetMethod("RefNotFoundTypeResponseGeneric", BindingFlags.Public | BindingFlags.Instance);
                    var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric
                        .MakeGenericMethod(type.GenericTypeArguments);
                    var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                    return onSuccess((object)dele);
                },
                referencedDocDoesNotExists.IsDefaultOrNull());
        }

        public class CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>
        {
            private Func<Type, TResult> referencedDocDoesNotExists;
            private HttpApplication thisAgain;
            private HttpRequestMessage requestAgain;
            private ParameterInfo paramInfo;
            private Func<object, Task<HttpResponseMessage>> onSuccess;
            
            public CallbackWrapperReferencedDocumentDoesNotExistsResponse(Func<Type, TResult> referencedDocDoesNotExists,
                HttpApplication thisAgain, HttpRequestMessage requestAgain, ParameterInfo paramInfo, Func<object, Task<HttpResponseMessage>> onSuccess)
            {
                this.referencedDocDoesNotExists = referencedDocDoesNotExists;
                this.thisAgain = thisAgain;
                this.requestAgain = requestAgain;
                this.paramInfo = paramInfo;
                this.onSuccess = onSuccess;
            }

            public HttpResponseMessage RefNotFoundTypeResponseGeneric<TResource>()
            {
                if (referencedDocDoesNotExists.IsDefaultOrNull())
                    return FailureToOverride<TResource>(typeof(ReferencedDocumentDoesNotExistsResponse<>), thisAgain, requestAgain, paramInfo, onSuccess);

                var result = referencedDocDoesNotExists(typeof(TResource));
                return new AttachedHttpResponseMessage<TResult>(result);
            }
        }

        private class InstigatorGenericWrapper1<TCallback, TResult, TResource>
        {
            private Type type;
            private HttpApplication httpApp;
            private HttpRequestMessage request;
            private ParameterInfo paramInfo;
            private TCallback callback;
            private Func<object, Task<HttpResponseMessage>> onSuccess;

            public InstigatorGenericWrapper1(Type type,
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo,
                TCallback callback, Func<object, Task<HttpResponseMessage>> onSuccess)
            {
                this.type = type;
                this.httpApp = httpApp;
                this.request = request;
                this.paramInfo = paramInfo;
                this.callback = callback;
                this.onSuccess = onSuccess;
            }

            HttpResponseMessage ContentTypeResponse(object content, string mediaType = default(string))
            {
                if (callback.IsDefault())
                    return FailureToOverride<TResource>(
                        type, this.httpApp, this.request, this.paramInfo, onSuccess);
                var contentObj = (object)content;
                var contentType = (TResource)contentObj;
                var callbackObj = (object)callback;
                var callbackDelegate = (Delegate)callbackObj;
                var resultObj = callbackDelegate.DynamicInvoke(contentType, mediaType);
                var result = (TResult)resultObj;
                return new AttachedHttpResponseMessage<TResult>(result);
            }

            HttpResponseMessage CreatedBodyResponse(object content, string mediaType = default(string))
            {
                if (callback.IsDefault())
                    return FailureToOverride<TResource>(
                        type, this.httpApp, this.request, this.paramInfo, onSuccess);
                var contentObj = (object)content;
                var contentType = (TResource)contentObj;
                var callbackObj = (object)callback;
                var callbackDelegate = (Delegate)callbackObj;
                var resultObj = callbackDelegate.DynamicInvoke(contentType, mediaType);
                var result = (TResult)resultObj;
                return new AttachedHttpResponseMessage<TResult>(result);
            }
        }

        private static void CreatedBodyResponse<TResource, TResult>(this IApplication application,
            Func<TResource, string, TResult> onCreated)
        {
            application.SetInstigatorGeneric(
                typeof(EastFive.Api.Controllers.CreatedBodyResponse<>),
                (type, httpApp, request, paramInfo, onSuccess) =>
                {
                    type = typeof(CreatedBodyResponse<>).MakeGenericType(typeof(TResource));
                    var wrapperConcreteType = typeof(InstigatorGenericWrapper1<,,>).MakeGenericType(
                        //type.GenericTypeArguments
                        //    .Append(typeof(Func<TResource, string, TResult>))
                        typeof(Func<TResource, string, TResult>)
                            .AsArray()
                            .Append(typeof(TResult))
                            .Append(typeof(TResource))
                            .ToArray());
                    var wrapperInstance = Activator.CreateInstance(wrapperConcreteType,
                        new object[] { type, httpApp, request, paramInfo, onCreated, onSuccess });
                    var dele = Delegate.CreateDelegate(type, wrapperInstance, "CreatedBodyResponse", false);
                    return onSuccess(dele);
                },
                onCreated.IsDefaultOrNull());
        }

        private static void ContentTypeResponse<TResource, TResult>(this IApplication application,
            Func<TResource, string, TResult> onCreated)
        {
            application.SetInstigatorGeneric(
                typeof(EastFive.Api.Controllers.ContentTypeResponse<>),
                (type, httpApp, request, paramInfo, onSuccess) =>
                {
                    type = typeof(ContentTypeResponse<>).MakeGenericType(typeof(TResource));
                    var wrapperConcreteType = typeof(InstigatorGenericWrapper1<,,>).MakeGenericType(
                        typeof(Func<TResource, string, TResult>)
                            .AsArray()
                            .Append(typeof(TResult))
                            .Append(typeof(TResource))
                            .ToArray());
                    var wrapperInstance = Activator.CreateInstance(wrapperConcreteType,
                        new object[] { type, httpApp, request, paramInfo, onCreated, onSuccess });
                    var dele = Delegate.CreateDelegate(type, wrapperInstance, "ContentTypeResponse", false);
                    return onSuccess(dele);
                },
                onCreated.IsDefaultOrNull());
        }

        private static void CreatedResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onCreated)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.CreatedResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.CreatedResponse created =
                        () =>
                        {
                            if (onCreated.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(EastFive.Api.Controllers.CreatedResponse),
                                    thisAgain, requestAgain, paramInfo, onSuccess);
                            return new AttachedHttpResponseMessage<TResult>(onCreated());
                        };
                    return onSuccess(created);
                },
                onCreated.IsDefaultOrNull());
        }

        private static void RedirectResponse<TResource, TResult>(this IApplication application,
            Func<Uri, string, TResult> onRedirect)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.RedirectResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.RedirectResponse redirect =
                        (where, why) =>
                        {
                            if (onRedirect.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(EastFive.Api.Controllers.RedirectResponse),
                                    thisAgain, requestAgain, paramInfo, onSuccess);
                            return new AttachedHttpResponseMessage<TResult>(onRedirect(where, why));
                        };
                    return onSuccess(redirect);
                });
        }

        private static void AlreadyExistsResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onAlreadyExists)
        {
            if (!onAlreadyExists.IsDefaultOrNull())
                application.SetInstigator(
                    typeof(EastFive.Api.Controllers.AlreadyExistsResponse),
                    (thisAgain, requestAgain, paramInfo, onSuccess) =>
                    {
                        EastFive.Api.Controllers.AlreadyExistsResponse exists =
                            () =>
                            {
                                if (onAlreadyExists.IsDefaultOrNull())
                                    return FailureToOverride<TResource>(
                                        typeof(EastFive.Api.Controllers.AlreadyExistsResponse),
                                        thisAgain, requestAgain, paramInfo, onSuccess);
                                return new AttachedHttpResponseMessage<TResult>(onAlreadyExists());
                            };
                        return onSuccess(exists);
                    });
        }


        private static void NotImplementedResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onNotImplemented)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.NotImplementedResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.NotImplementedResponse notImplemented =
                        () =>
                        {
                            if (onNotImplemented.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(EastFive.Api.Controllers.NotImplementedResponse),
                                    thisAgain, requestAgain, paramInfo, onSuccess);
                            return new AttachedHttpResponseMessage<TResult>(onNotImplemented());
                        };
                    return onSuccess(notImplemented);
                });
        }

        private static void MultipartContentResponse<TResource, TResult>(this IApplication application,
            Func<TResource[], TResult> onContents)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.MultipartAcceptArrayResponseAsync),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.MultipartAcceptArrayResponseAsync created =
                        (contents) =>
                        {
                            var resources = contents.Cast<TResource>().ToArray();
                            // TODO: try catch
                            //if (!(content is TResource))
                            //    Assert.Fail($"Could not cast {content.GetType().FullName} to {typeof(TResource).FullName}.");

                            if (onContents.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(EastFive.Api.Controllers.MultipartAcceptArrayResponseAsync), 
                                    thisAgain, requestAgain, paramInfo, onSuccess).AsTask();
                            var result = onContents(resources);
                            return new AttachedHttpResponseMessage<TResult>(result).ToTask<HttpResponseMessage>();
                        };
                    return onSuccess(created);
                });

            application.SetInstigatorGeneric(
                typeof(EastFive.Api.Controllers.MultipartResponseAsync<>),
                (type, thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    var callbackWrapperType = typeof(CallbackWrapper<,>).MakeGenericType(
                        paramInfo.ParameterType.GenericTypeArguments.Append(typeof(TResult)).ToArray());

                    //  new CallbackWrapper<TResource, TResult>(onContents, null, thisAgain, requestAgain, paramInfo, onSuccess);
                    var instantiationParams = new object[]
                        {
                            onContents,
                            null,
                            thisAgain,
                            requestAgain,
                            paramInfo,
                            onSuccess,
                        };
                    var scope = Activator.CreateInstance(callbackWrapperType, instantiationParams);

                    var multipartResponseMethodInfoGeneric = callbackWrapperType.GetMethod("MultipartResponseAsyncGeneric", BindingFlags.Public | BindingFlags.Instance);
                    var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric;
                    var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                    return onSuccess((object)dele);
                });
        }

        private static void MultipartContentObjectResponse<TResource, TResult>(this IApplication application,
            Func<object[], TResult> onContents)
        {
            application.SetInstigator(
                typeof(EastFive.Api.Controllers.MultipartAcceptArrayResponseAsync),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    EastFive.Api.Controllers.MultipartAcceptArrayResponseAsync created =
                        (contents) =>
                        {
                            var resources = contents.ToArray();
                            // TODO: try catch
                            //if (!(content is TResource))
                            //    Assert.Fail($"Could not cast {content.GetType().FullName} to {typeof(TResource).FullName}.");

                            if (onContents.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(EastFive.Api.Controllers.MultipartAcceptArrayResponseAsync),
                                    thisAgain, requestAgain, paramInfo, onSuccess).AsTask();
                            var result = onContents(resources);
                            return new AttachedHttpResponseMessage<TResult>(result).ToTask<HttpResponseMessage>();
                        };
                    return onSuccess(created);
                });

            application.SetInstigatorGeneric(
                typeof(EastFive.Api.Controllers.MultipartResponseAsync<>),
                (type, thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    var callbackWrapperInstance = typeof(CallbackWrapper<,>).MakeGenericType(
                        new Type[] { type.GenericTypeArguments.First(), typeof(TResult) });
                    //var scope = new CallbackWrapper<TResource, TResult>(null, onContents, thisAgain, requestAgain, paramInfo, onSuccess);
                    var scope = Activator.CreateInstance(callbackWrapperInstance, 
                        new object[] { null, onContents, thisAgain, requestAgain, paramInfo, onSuccess });
                    var multipartResponseMethodInfoGeneric = callbackWrapperInstance.GetMethod("MultipartResponseAsyncGeneric", BindingFlags.Public | BindingFlags.Instance);
                    var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric; // multipartResponseMethodInfoGeneric.MakeGenericMethod(type.GenericTypeArguments);
                    var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                    return onSuccess((object)dele);
                });
        }

        public class CallbackWrapper<TResource, TResult>
        {
            private Func<TResource[], TResult> callback;
            private Func<object[], TResult> callbackObjs;
            private HttpApplication thisAgain;
            private HttpRequestMessage requestAgain;
            private ParameterInfo paramInfo;
            private Func<object, Task<HttpResponseMessage>> onSuccess;
            
            public CallbackWrapper(Func<TResource[], TResult> onContents, Func<object[], TResult> callbackObjs,
                HttpApplication thisAgain, HttpRequestMessage requestAgain, ParameterInfo paramInfo,
                Func<object, Task<HttpResponseMessage>> onSuccess)
            {
                this.callback = onContents;
                this.callbackObjs = callbackObjs;
                this.thisAgain = thisAgain;
                this.requestAgain = requestAgain;
                this.paramInfo = paramInfo;
                this.onSuccess = onSuccess;
            }

            public async Task<HttpResponseMessage> MultipartResponseAsyncGeneric(IEnumerableAsync<TResource> resources)
            {
                if (!callback.IsDefaultOrNull())
                {
                    var resourcesArray = await resources.ToArrayAsync();
                    var result = callback(resourcesArray);
                    return new AttachedHttpResponseMessage<TResult>(result);
                }
                if (!callbackObjs.IsDefaultOrNull())
                {
                    var resourcesArray = await resources.ToArrayAsync();
                    var result = callbackObjs(resourcesArray.Cast<object>().ToArray());
                    return new AttachedHttpResponseMessage<TResult>(result);
                }
                return FailureToOverride<TResource>(typeof(EastFive.Api.Controllers.MultipartResponseAsync<>), 
                    thisAgain, requestAgain, paramInfo, onSuccess);
            }
            
        }

        private static void NoContentResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onNoContent)
        {
            application.SetInstigator(
                typeof(NoContentResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    NoContentResponse created =
                        () =>
                        {
                            if (onNoContent.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(NoContentResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onNoContent();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        private static void NotFoundResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onNotFound)
        {
            application.SetInstigator(
                typeof(NotFoundResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    NotFoundResponse notFound =
                        () =>
                        {
                            if (onNotFound.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(NotFoundResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onNotFound();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(notFound);
                });
        }

        private static void UnauthorizedResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onUnauthorized)
        {
            application.SetInstigator(
                typeof(UnauthorizedResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    UnauthorizedResponse created =
                        () =>
                        {
                            if (onUnauthorized.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(UnauthorizedResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onUnauthorized();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        private static void GeneralConflictResponse<TResource, TResult>(this IApplication application,
            Func<string, TResult> onGeneralConflictResponse)
        {
            application.SetInstigator(
                typeof(GeneralConflictResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    GeneralConflictResponse created =
                        (reason) =>
                        {
                            if (onGeneralConflictResponse.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(GeneralConflictResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onGeneralConflictResponse(reason);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
               });
        }

        private static void ExecuteBackgroundResponse<TResource, TResult>(this IApplication application,
            Func<IExecuteAsync, Task<TResult>> onExecuteBackgroundResponse)
        {
            application.SetInstigator(
                typeof(ExecuteBackgroundResponseAsync),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    ExecuteBackgroundResponseAsync created =
                        async (executionContent) =>
                        {
                            if (onExecuteBackgroundResponse.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(ExecuteBackgroundResponseAsync), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = await onExecuteBackgroundResponse(executionContent);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }


        private static HttpResponseMessage FailureToOverride<TResource>(Type typeOfResponse,
            HttpApplication application,
            HttpRequestMessage request, ParameterInfo paramInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            return new NoOverrideHttpResponseMessage<TResource>(paramInfo.ParameterType, request);
        }

        #endregion


        public static async Task<TResult> UrlAsync<TResource, TResultInner, TResult>(this IInvokeApplication invokeApplication,
                HttpMethod method, Uri location,
            Func<TResultInner, TResult> onExecuted,

            Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<object[], TResult> onContentObjects = default(Func<object[], TResult>),
            Func<string, TResult> onHtml = default(Func<string, TResult>),
            Func<TResult> onCreated = default(Func<TResult>),
            Func<TResource, string, TResult> onCreatedBody = default(Func<TResource, string, TResult>),
            Func<TResult> onUpdated = default(Func<TResult>),

            Func<Uri, string, TResult> onRedirect = default(Func<Uri, string, TResult>),

            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onUnauthorized = default(Func<TResult>),
            Func<TResult> onExists = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<string, TResult> onFailure = default(Func<string, TResult>),

            Func<TResult> onNotImplemented = default(Func<TResult>),
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default(Func<IExecuteAsync, Task<TResult>>))
        {
            throw new NotImplementedException();
            var request = invokeApplication.GetRequest<TResource>();
            //request.Method = method;
            //request.RequestUri = location;
            var application = request.Application;

            application.CreatedResponse<TResource, TResult>(onCreated);
            application.CreatedBodyResponse<TResource, TResult>(onCreatedBody);
            application.BadRequestResponse<TResource, TResult>(onBadRequest);
            application.AlreadyExistsResponse<TResource, TResult>(onExists);
            application.RefNotFoundTypeResponse(onRefDoesNotExistsType);
            application.RedirectResponse<TResource, TResult>(onRedirect);
            application.NotImplementedResponse<TResource, TResult>(onNotImplemented);

            application.ContentResponse(onContent);
            application.ContentTypeResponse<TResource, TResult>((body, contentType) => onContent(body));
            application.MultipartContentResponse(onContents);
            if (!onContentObjects.IsDefaultOrNull())
                application.MultipartContentObjectResponse<TResource, TResult>(onContentObjects);
            application.NotFoundResponse<TResource, TResult>(onNotFound);
            application.HtmlResponse<TResource, TResult>(onHtml);

            application.NoContentResponse<TResource, TResult>(onUpdated);
            application.UnauthorizedResponse<TResource, TResult>(onUnauthorized);
            application.GeneralConflictResponse<TResource, TResult>(onFailure);
            application.ExecuteBackgroundResponse<TResource, TResult>(onExecuteBackground);

            var response = await request.SendAsync(request.Request);

            if (response is IDidNotOverride)
                (response as IDidNotOverride).OnFailure();

            if (!(response is IReturnResult))
                throw new Exception($"Failed to override response with status code `{response.StatusCode}` for {typeof(TResource).FullName}\nResponse:{response.ReasonPhrase}");

            var attachedResponse = response as IReturnResult;
            var result = attachedResponse.GetResultCasted<TResultInner>();
            return onExecuted(result);
        }
    }
}
