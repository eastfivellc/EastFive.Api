using System;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Services;
using EastFive.Linq.Expressions;
using EastFive.Collections.Generic;
using BlackBarLabs.Extensions;

namespace BlackBarLabs.Api.Controllers
{
    public class BaseController : ApiController
    {
        protected BaseController()
        {

        }

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
        }
    }
}

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field)]
    public class HttpPostAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HttpGetAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HttpPutAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HttpDeleteAttribute : Attribute
    {

    }
}

namespace EastFive.Api.Controllers
{
    public delegate HttpResponseMessage GeneralConflictResponse(string value);
    public delegate HttpResponseMessage CreatedResponse();
    public delegate HttpResponseMessage AlreadyExistsResponse();
    public delegate HttpResponseMessage AlreadyExistsReferencedResponse(Guid value);
    public delegate HttpResponseMessage NoContentResponse();
    public delegate HttpResponseMessage AcceptedResponse();
    public delegate HttpResponseMessage NotFoundResponse();
    public delegate HttpResponseMessage ContentResponse(object content);
    public delegate Task<HttpResponseMessage> MultipartResponseAsync(IEnumerable<HttpResponseMessage> responses);
    public delegate HttpResponseMessage ReferencedDocumentNotFoundResponse();
    public delegate HttpResponseMessage UnauthorizedResponse();
    public delegate HttpResponseMessage NotModifiedResponse();
    
    public static class ApiValidations
    {
        [AttributeUsage(AttributeTargets.Method)]
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
        public static bool ParamDatetimeAny(this DateTimeQuery sourceValue)
        {

            return sourceValue.ParseInternal(
                (v1, v2) => { throw new Exception("ParamDatetimeAny for DateTimeQuery matched range."); },
                (v) => true,
                () => true,
                () => { throw new Exception("ParamDatetimeAny for DateTimeQuery matched empty."); },
                () => { throw new Exception("ParamDatetimeAny for DateTimeQuery matched invalid value."); });
        }

        [ValidationUnspecified]
        public static bool ParamDatetimeEmpty(this DateTimeQuery sourceValue)
        {
            return sourceValue.ParseInternal(
                (v1, v2) => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched range."); },
                (v) => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched value."); },
                () => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched any."); },
                () => false,
                () => { throw new Exception("ParamDatetimeEmpty for DateTimeQuery matched invalid value."); });
        }
    }

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
                        AlreadyExistsReferencedResponse dele = (why) => controller.Request.CreateAlreadyExistsResponse<ApiController>(why, controller.Url);
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
    {
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

        protected ApiController()
            : base()
        {

        }

        public IHttpActionResult Post([FromBody]TResource resource)
        {
            return new HttpActionResult(() => Invoke<TResource, HttpPostAttribute>(resource, new PropertyInfo[] { }));
        }

        public IHttpActionResult Get([FromUri]TQuery resource)
        {
            return new HttpActionResult(() => GetQueryObjectParamters(resource, this.Request,
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


        internal static async Task<HttpResponseMessage> GetQueryObjectParamters(TQuery query, HttpRequestMessage request,
            Func<PropertyInfo[], Task<HttpResponseMessage>> callback)
        {
            if (query.IsDefault())
            {
                var emptyQuery = Activator.CreateInstance<TQuery>();
                if (emptyQuery.IsDefault())
                    throw new Exception($"Could not activate object of type {typeof(TQuery).FullName}");
                return await GetQueryObjectParamters(emptyQuery, request, callback);
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