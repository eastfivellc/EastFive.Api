using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Api.Controllers
{
    public class ApiController : System.Web.Http.ApiController
    {
        public delegate ApiValidations.ValidationAttribute ParseInputDelegate(object v, ApiController controller);
        
        protected static Dictionary<Type, Dictionary<Type, ParseInputDelegate>> paramFunctions =
            new Dictionary<Type, Dictionary<Type, ParseInputDelegate>>()
            {
                {
                    typeof(WebId),
                    new Dictionary<Type, ParseInputDelegate>()
                    {
                        { typeof(Guid), ParseWebIdGuid },
                    }
                },
                {
                    typeof(WebIdQuery),
                    new Dictionary<Type, ParseInputDelegate>()
                    {
                        { typeof(Guid), ParseWebIdQueryGuid },
                        { typeof(Guid[]), ParseWebIdQueryGuid },
                    }
                },
                {
                    typeof(DateTimeQuery),
                    new Dictionary<Type, ParseInputDelegate>()
                    {
                        { typeof(bool), ParseDateTimeBool },
                    }
                },
            };

        protected static Dictionary<Type, Func<ApiController, Func<object, Task<HttpResponseMessage>>, Task<HttpResponseMessage>>> instigators =
            new Dictionary<Type, Func<ApiController, Func<object, Task<HttpResponseMessage>>, Task<HttpResponseMessage>>>()
            {
                {
                    typeof(Security),
                    (controller, success) => controller.Request.GetActorIdClaimsAsync(
                        (actorId, claims) => success(
                            new Security
                            {
                                performingAsActorId = actorId,
                                claims = claims,
                            }))
                },
                {
                    typeof(System.Web.Http.Routing.UrlHelper),
                    (controller, success) => success(controller.Url)
                },
                {
                    typeof(HttpRequestMessage),
                    (controller, success) => success(controller.Request)
                },
                {
                    typeof(GeneralConflictResponse),
                    (controller, success) =>
                    {
                        GeneralConflictResponse dele = (why) => controller.Request.CreateResponse(System.Net.HttpStatusCode.Conflict).AddReason(why);
                        return success((object)dele);
                    }
                },
                {
                    typeof(GeneralFailureResponse),
                    (controller, success) =>
                    {
                        GeneralFailureResponse dele = (why) => controller.Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError).AddReason(why);
                        return success((object)dele);
                    }
                },
                {
                    typeof(AlreadyExistsResponse),
                    (controller, success) =>
                    {
                        AlreadyExistsResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.Conflict).AddReason("Resource has already been created");
                        return success((object)dele);
                    }
                },
                {
                    typeof(AlreadyExistsReferencedResponse),
                    (controller, success) =>
                    {
                        AlreadyExistsReferencedResponse dele = (why) => controller.Request.CreateAlreadyExistsResponse(controller.GetType(), why, controller.Url);
                        return success((object)dele);
                    }
                },
                {
                    typeof(NoContentResponse),
                    (controller, success) =>
                    {
                        NoContentResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.NoContent);
                        return success((object)dele);
                    }
                },
                {
                    typeof(NotFoundResponse),
                    (controller, success) =>
                    {
                        NotFoundResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.NotFound);
                        return success((object)dele);
                    }
                },
                {
                    typeof(ContentResponse),
                    (controller, success) =>
                    {
                        ContentResponse dele = (obj) => controller.Request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                        return success((object)dele);
                    }
                },
                {
                    typeof(MultipartResponseAsync),
                    (controller, success) =>
                    {
                        MultipartResponseAsync dele = (responses) => controller.Request.CreateMultipartResponseAsync(responses);
                        return success((object)dele);
                    }
                },
                {
                    typeof(MultipartAcceptResponseAsync),
                    (controller, success) =>
                    {
                        MultipartAcceptResponseAsync dele =
                            (objects) =>
                            {
                                if (controller.Request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
                                {
                                    return controller.Request.CreateXlsxResponse(
                                        new Dictionary<string, string>(),
                                        objects).ToTask();
                                }
                                var responses = objects.Select(obj => controller.Request.CreateResponse(System.Net.HttpStatusCode.OK, obj));
                                return controller.Request.CreateMultipartResponseAsync(responses);
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(MultipartAcceptArrayResponseAsync),
                    (controller, success) =>
                    {
                        MultipartAcceptArrayResponseAsync dele =
                            (objects) =>
                            {
                                if (controller.Request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
                                {
                                    return controller.Request.CreateMultisheetXlsxResponse(
                                        new Dictionary<string, string>(),
                                        objects.Cast<ResourceBase>()).ToTask();
                                }
                                var responses = objects.Select(obj => controller.Request.CreateResponse(System.Net.HttpStatusCode.OK, obj));
                                return controller.Request.CreateMultipartResponseAsync(responses);
                            };
                        return success((object)dele);
                    }
                },
                {
                    typeof(ReferencedDocumentNotFoundResponse),
                    (controller, success) =>
                    {
                        ReferencedDocumentNotFoundResponse dele = () => controller.Request
                            .CreateResponse(System.Net.HttpStatusCode.BadRequest)
                            .AddReason("The query parameter did not reference an existing document.");
                        return success((object)dele);
                    }
                },
                {
                    typeof(UnauthorizedResponse),
                    (controller, success) =>
                    {
                        UnauthorizedResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
                        return success((object)dele);
                    }
                },
                {
                    typeof(AcceptedResponse),
                    (controller, success) =>
                    {
                        AcceptedResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.Accepted);
                        return success((object)dele);
                    }
                },
                {
                    typeof(NotModifiedResponse),
                    (controller, success) =>
                    {
                        NotModifiedResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.NotModified);
                        return success((object)dele);
                    }
                },
                {
                    typeof(CreatedResponse),
                    (controller, success) =>
                    {
                        CreatedResponse dele = () => controller.Request.CreateResponse(System.Net.HttpStatusCode.Created);
                        return success((object)dele);
                    }
                },
            };

        public static void AddInstigator(Type type, Func<ApiController, Func<object, Task<HttpResponseMessage>>, Task<HttpResponseMessage>> instigator)
        {
            instigators.Add(type, instigator);
        }

        private static ApiValidations.ValidationAttribute ParseWebIdGuid(object v, ApiController controller)
        {
            if (v.IsDefaultOrNull())
                return new ApiValidations.ValidationDefaultAttribute();

            var webId = v as WebId;
            if (webId.UUID.IsDefaultOrEmpty())
                return new ApiValidations.ValidationDefaultAttribute();

            return new ApiValidations.ValidationValueAttribute();
        }

        private static ApiValidations.ValidationAttribute ParseWebIdQueryGuid(object v, ApiController controller)
        {
            var webIdQueryValue = (WebIdQuery)v;
            if (v.IsDefaultOrNull())
                return new ApiValidations.ValidationUnspecified();
            return webIdQueryValue.Parse2<ApiValidations.ValidationAttribute>(controller.Request,
                (id) => new ApiValidations.ValidationValueAttribute(),
                (ids) => new ApiValidations.ValidationMultipleAttribute(),
                () => new ApiValidations.ValidationDefaultAttribute(),
                () => new ApiValidations.ValidationDefaultAttribute());
        }

        private static ApiValidations.ValidationAttribute ParseDateTimeBool(object v, ApiController controller)
        {
            var dateTimeQuery = (DateTimeQuery)v;
            return dateTimeQuery.ParseInternal<ApiValidations.ValidationAttribute>(
                    (v1, v2) => new ApiValidations.ValidationRangeAttribute(),
                    (vDateTime) => new ApiValidations.ValidationValueAttribute(),
                    () => new ApiValidations.ValidationAnyAttribute(),
                    () => new ApiValidations.ValidationUnspecified(),
                    () => new ApiValidations.ValidationInvalidAttribute());
        }

        protected ApiController()
            : base()
        {

        }

        public struct Security
        {
            public Guid performingAsActorId;
            public System.Security.Claims.Claim[] claims;
        }
    }

    public class ApiController<TResource, TQuery> : ApiController
        where TResource : BlackBarLabs.Api.ResourceBase
    {
        [ApiValidations.ValidationValue]
        public delegate Guid ValidGuid(Expression<Func<TResource, WebId>> expression);

        public delegate BlackBarLabs.Api.Resources.WebIdQuery WebIdValueValidation(TQuery query);
        public delegate BlackBarLabs.Api.Resources.DateTimeQuery DateTimeEmptyValidation(TQuery query);

        private static Func<ValidatedResponse> _newValidatedResponse;
        
        protected sealed class ValidatedResponse
        {
            static ValidatedResponse()
            {
                _newValidatedResponse = () => new ValidatedResponse();
            }

            private ValidatedResponse()
            {
                
            }
        }

        protected delegate ValidatedResponse Validation<T1, V1>(
            Expression<T1> validation1,
            Func<V1, Task<HttpResponseMessage>> callback);

        protected delegate ValidatedResponse Validation<T1, V1, T2, V2>(
            Expression<T1> validation1,
            Expression<T2> validation2,
            Func<V1, V2, Task<HttpResponseMessage>> callback);

                // Func<
                //Expression<WebIdValueValidation>,
                //Expression<DateTimeEmptyValidation>,
                //Func<Guid, DateTime?, Task<HttpResponseMessage>>,
                //Task<HttpResponseMessage>> validate)

        public delegate Task<HttpResponseMessage> RequestGetById<TContext>(
                Security security,
                TContext context,
                TQuery query,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper url,
                ContentResponse onContent,
                NotFoundResponse onNotFound,
                UnauthorizedResponse onUnauthorized);

        public delegate Task<HttpResponseMessage> RequestGetByIdWithFailure<TContext>(
                Security security,
                TContext context,
                TQuery query,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper url,
                ContentResponse onContent,
                NotFoundResponse onNotFound,
                UnauthorizedResponse onUnauthorized,
                GeneralFailureResponse onFailure);

        public delegate Task<Task<HttpResponseMessage>> RequestGetMultiple<TContext>(
                Security security,
                TContext context,
                TQuery query,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper url,
                ContentResponse onContent,
                MultipartResponseAsync onMultipart,
                ReferencedDocumentNotFoundResponse onQueryByDoc,
                UnauthorizedResponse onUnauthorized);

        public delegate Task<Task<HttpResponseMessage>> RequestGetMultipleAccept<TContext>(
                Security security,
                TContext context,
                TQuery query,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper url,
                ContentResponse onContent,
                MultipartAcceptResponseAsync onMultipart,
                ReferencedDocumentNotFoundResponse onQueryByDoc,
                UnauthorizedResponse onUnauthorized);

        public delegate Task<Task<HttpResponseMessage>> RequestGetMultipleArrayAccept<TContext>(
                Security security,
                TContext context,
                TQuery query,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper url,
                ContentResponse onContent,
                MultipartAcceptArrayResponseAsync onMultipart,
                ReferencedDocumentNotFoundResponse onQueryByDoc,
                UnauthorizedResponse onUnauthorized);

        public delegate Task<HttpResponseMessage> ParseXlsxDelegate(
                 Func<KeyValuePair<TResource[], KeyValuePair<string, string>[]>[], Task<HttpResponseMessage>> execute);

        public delegate Task<HttpResponseMessage> ParseXlsxMultipartDelegate(
                 Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePost,
                 Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePut);

        private void AddGenericInstigator(Type type, Func<ApiController, Func<object, Task<HttpResponseMessage>>, Task<HttpResponseMessage>> instigator)
        {
            if (instigators.ContainsKey(type))
                return;
            AddInstigator(
                type,
                instigator);
        }

        protected ApiController()
            : base()
        {
            AddGenericInstigator(
                typeof(ParseXlsxDelegate),
                async (controller, success) =>
                {
                    return await await controller.Request.Content.ParseMultipartAsync(
                        (System.IO.Stream xlsx) => ParseSheetAsync(controller, xlsx, success),
                        () => controller.Request
                            .CreateResponse(System.Net.HttpStatusCode.BadRequest, "xlsx file was not provided")
                            .ToTask());
                });

            AddGenericInstigator(
                typeof(ParseXlsxMultipartDelegate),
                async (controller, success) =>
                {
                    return await await controller.Request.Content.ParseMultipartAsync(
                        (System.IO.Stream xlsx) => ParseSheetMultipartAsync(controller, xlsx, success),
                        () => controller.Request
                            .CreateResponse(System.Net.HttpStatusCode.BadRequest, "xlsx file was not provided")
                            .ToTask());
                });
        }

        private static Task<HttpResponseMessage> ParseSheetAsync(ApiController controller, System.IO.Stream xlsx, Func<object, Task<HttpResponseMessage>> success)
        {
            ParseXlsxDelegate dele =
                (execute) =>
                {
                    return controller.Request.ParseXlsx(xlsx, execute);
                };
            return success(dele);
        }

        private static Task<HttpResponseMessage> ParseSheetMultipartAsync(ApiController controller, System.IO.Stream xlsx, Func<object, Task<HttpResponseMessage>> success)
        {
            ParseXlsxMultipartDelegate dele =
                async (executePost, executePut) =>
                {
                    return await await controller.Request.ParseXlsxAsync(xlsx, executePost, executePut,
                        responses => controller.Request.CreateMultipartResponseAsync(responses));
                };
            return success(dele);
        }

        //public IHttpActionResult Post([FromBody]TResource resource)
        //{
        //    return new HttpActionResult(() => Invoke<TResource, HttpPostAttribute>(resource, new PropertyInfo[] { }));
        //}

        public IHttpActionResult Post([FromUri]TQuery resource)
        {
            return new HttpActionResult(() => GetQueryObjectParameters(resource, this.Request,
                async (validations) =>
                {
                    return await await GetBodyObjectParameters(this.Request.Content.ParseMultipartAsync(
                        (System.IO.Stream offers) => offers.CreatePriceSheetAsync(this.Request, this.Url),
                        async () =>
                        {
                            var contentString = await this.Request.Content.ReadAsStringAsync();
                            var create = Newtonsoft.Json.JsonConvert.DeserializeObject<Resources.PriceSheetProductOffer>(contentString);
                            return await create.CreateAsync(this.Request, this.Url);
                        }));
                    return Invoke<TQuery, HttpGetAttribute>(resource, validations);
                }));
        }

        public IHttpActionResult Get([FromUri]TQuery resource)
        {
            return new HttpActionResult(() => GetQueryObjectParameters(resource, this.Request,
                (validations) =>
                {
                    return Invoke<TQuery, HttpGetAttribute>(resource, validations);
                }));
        }

        public IHttpActionResult Put([FromBody]TResource resource)
        {
            return new HttpActionResult(() => Invoke<TResource, HttpPutAttribute>(resource, new PropertyInfo[] { }));
        }

        public IHttpActionResult Delete([FromUri]TQuery resource)
        {
            return new HttpActionResult(() => Invoke<TQuery, HttpDeleteAttribute>(resource, new PropertyInfo[] { }));
        }

        private Task<HttpResponseMessage> Invoke<T, TAttribute>(T resource, PropertyInfo[] mustMatchProperties)
            where TAttribute : System.Attribute
        {
            var responseMessage = this.GetType()
                .GetFields()
                .Where(field => field.ContainsCustomAttribute<TAttribute>())
                .Where(field => field.FieldType.IsSubClassOfGeneric(typeof(Expression<>)))
                .Aggregate(
                    new
                    {
                        response = default(Task<HttpResponseMessage>),
                        unvalidateds = new MemberInfo[][] { },
                    },
                    (aggr, propertyType) =>
                    {
                        if (!aggr.response.IsDefault())
                            return aggr;

                        var expression = propertyType.GetValue(this);
                        if (!(expression is LambdaExpression))
                            return aggr;
                        var lambdaExpr = expression as LambdaExpression;
                        var parameters = lambdaExpr.Parameters;
                        var body = lambdaExpr.Body;
                        if (!(body is MethodCallExpression))
                            return aggr;

                        var doubleAwait = false;
                        var methodBody = body as MethodCallExpression;
                        if (!typeof(Task<HttpResponseMessage>).IsAssignableFrom(methodBody.Method.ReturnType))
                        {
                            if (!typeof(Task<Task<HttpResponseMessage>>).IsAssignableFrom(methodBody.Method.ReturnType))
                                return aggr;
                            doubleAwait = true;
                        }

                        var unvalidatedProperties = methodBody.Arguments
                            .Where(argument => argument is MethodCallExpression)
                            .Select(argument => argument as MethodCallExpression)
                            .Aggregate(mustMatchProperties.Cast<MemberInfo>(),
                                (props, methodCallExpression) =>
                                {
                                    return methodCallExpression.Method.GetCustomAttribute(
                                        (ApiValidations.ValidationAttribute validationAttr) =>
                                        {
                                            // TODO: Catch convert here
                                            var memberLookupArgumentsMatchingResourceType = methodCallExpression.Arguments
                                                .Where(arg => arg is MemberExpression)
                                                .Where(arg => (arg as MemberExpression).Member is MemberInfo)
                                                .Where(arg => ((arg as MemberExpression).Member as MemberInfo).ReflectedType.IsAssignableFrom(typeof(T)));
                                            if (!memberLookupArgumentsMatchingResourceType.Any())
                                                return props;
                                            var memberLookupArgument = memberLookupArgumentsMatchingResourceType.First();
                                            var member = ((memberLookupArgument as MemberExpression).Member as MemberInfo);
                                            var v = member.GetValue(resource);
                                            var conversionMethod = methodCallExpression.Method;
                                            var validationFunction = paramFunctions[memberLookupArgument.Type][conversionMethod.ReturnType];
                                            var validationResult = validationFunction(v, this);
                                            if (validationResult.GetType() != validationAttr.GetType())
                                                return props.Append(member).ToArray();

                                            var reducedProps = props
                                                .Where(prop => prop.Name != member.Name)
                                                .ToArray();
                                            return reducedProps;
                                        },
                                        () => props);
                                });

                        if (unvalidatedProperties.Any())
                            return new
                            {
                                response = default(Task<HttpResponseMessage>),
                                unvalidateds = aggr.unvalidateds.Append(unvalidatedProperties.ToArray()).ToArray(),
                            };
                        
                        var response = parameters
                            .Aggregate<ParameterExpression, object[], Task<HttpResponseMessage>>(new object[] { },
                                (paramValues, param, next) =>
                                {
                                    if (param.Type == typeof(T))
                                        return next(paramValues.Append(resource).ToArray());
                                    if (instigators.ContainsKey(param.Type))
                                        return instigators[param.Type](this,
                                            (v) => next(paramValues.Append(v).ToArray()));
                                    return Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                                        .AddReason($"Could not instatiate type: {param.Type.FullName}")
                                        .ToTask();
                                },
                                async (paramValues) =>
                                {
                                    // Expression.Call()
                                    // lambdaExpr.CompileToMethod()
                                    if (doubleAwait)
                                        return await await (Task<Task<HttpResponseMessage>>)lambdaExpr.Compile().DynamicInvoke(paramValues);
                                    return await (Task<HttpResponseMessage>)lambdaExpr.Compile().DynamicInvoke(paramValues);
                                });
                        return new
                        {
                            response = response,
                            unvalidateds = aggr.unvalidateds,
                        };
                    },
                    (aggr) =>
                    {
                        if (!aggr.response.IsDefaultOrNull())
                            return aggr.response;
                        var content = $"Please include a value for one of [{aggr.unvalidateds.Select(uvs => uvs.Select(uv => uv.Name).Join(",")).Join(" or ")}]";
                        return Request
                            .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                            .AddReason(content)
                            .ToTask();
                    });

            return responseMessage;
        }
        
        internal static async Task<HttpResponseMessage> GetQueryObjectParameters(TQuery query, HttpRequestMessage request,
            Func<PropertyInfo[], Task<HttpResponseMessage>> callback)
        {
            if (query.IsDefault())
            {
                var emptyQuery = Activator.CreateInstance<TQuery>();
                if (emptyQuery.IsDefault())
                    throw new Exception($"Could not activate object of type {typeof(TQuery).FullName}");
                return await GetQueryObjectParameters(emptyQuery, request, callback);
            }

            if (query is ResourceQueryBase)
            {
                var resourceQuery = query as ResourceQueryBase;
                if (resourceQuery.Id.IsDefault() &&
                   String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
                   request.RequestUri.Segments.Any())
                {
                    var idRefQuery = request.RequestUri.Segments.Last();
                    Guid idRefGuid;
                    if (Guid.TryParse(idRefQuery, out idRefGuid))
                        resourceQuery.Id = idRefGuid;
                }
            }

            if (query is ResourceBase)
            {
                var resource = query as ResourceBase;
                if (resource.Id.IsDefault() &&
                   String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
                   request.RequestUri.Segments.Any())
                {
                    var idRefQuery = request.RequestUri.Segments.Last();
                    Guid idRefGuid;
                    if (Guid.TryParse(idRefQuery, out idRefGuid))
                        resource.Id = idRefGuid;
                }
            }

            return await query.GetType().GetProperties()
                .Aggregate<PropertyInfo, PropertyInfo[], Task<HttpResponseMessage>>(
                    (new PropertyInfo[] { }),
                    (properties, prop, next) =>
                    {
                        var value = prop.GetValue(query);
                        if (null == value)
                            return next(properties);
                        if (typeof(IQueryParameter).IsInstanceOfType(value))
                        {
                            return ((IQueryParameter)value).Parse(
                                (v) => next(properties.Append(prop).ToArray()),
                                (why) => request.CreateResponse(System.Net.HttpStatusCode.BadRequest).AddReason(why).ToTask());
                        }
                        return next(properties);
                    },
                    (kvps) => callback(kvps));
        }

        internal static async Task<HttpResponseMessage> GetBaseObjectParameters(TQuery query, HttpRequestMessage request,
            Func<PropertyInfo[], Task<HttpResponseMessage>> callback)
        {
            if (query.IsDefault())
            {
                var emptyQuery = Activator.CreateInstance<TQuery>();
                if (emptyQuery.IsDefault())
                    throw new Exception($"Could not activate object of type {typeof(TQuery).FullName}");
                return await GetQueryObjectParameters(emptyQuery, request, callback);
            }

            if (query is ResourceQueryBase)
            {
                var resourceQuery = query as ResourceQueryBase;
                if (resourceQuery.Id.IsDefault() &&
                   String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
                   request.RequestUri.Segments.Any())
                {
                    var idRefQuery = request.RequestUri.Segments.Last();
                    Guid idRefGuid;
                    if (Guid.TryParse(idRefQuery, out idRefGuid))
                        resourceQuery.Id = idRefGuid;
                }
            }

            if (query is ResourceBase)
            {
                var resource = query as ResourceBase;
                if (resource.Id.IsDefault() &&
                   String.IsNullOrWhiteSpace(request.RequestUri.Query) &&
                   request.RequestUri.Segments.Any())
                {
                    var idRefQuery = request.RequestUri.Segments.Last();
                    Guid idRefGuid;
                    if (Guid.TryParse(idRefQuery, out idRefGuid))
                        resource.Id = idRefGuid;
                }
            }

            return await query.GetType().GetProperties()
                .Aggregate<PropertyInfo, PropertyInfo[], Task<HttpResponseMessage>>(
                    (new PropertyInfo[] { }),
                    (properties, prop, next) =>
                    {
                        var value = prop.GetValue(query);
                        if (null == value)
                            return next(properties);
                        if (typeof(IQueryParameter).IsInstanceOfType(value))
                        {
                            return ((IQueryParameter)value).Parse(
                                (v) => next(properties.Append(prop).ToArray()),
                                (why) => request.CreateResponse(System.Net.HttpStatusCode.BadRequest).AddReason(why).ToTask());
                        }
                        return next(properties);
                    },
                    (kvps) => callback(kvps));
        }
    }
}