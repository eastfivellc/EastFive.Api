using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Collections;
using EastFive.Collections.Generic;
using System.IO;
using EastFive.Api.Meta.Flows;

namespace EastFive.Api.Meta.Postman
{
    [FunctionViewController(
        Route = "PostmanCollection",
        Namespace = "meta")]
    //[OpenApiRoute(Collection = "EastFive.Api.Meta")]
    public class PostmanCollection : IReferenceable
    {
        #region Properties

        [JsonIgnore]
        public Guid id => this.postmanCollectionRef.id;

        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [ApiProperty(PropertyName = IdPropertyName)]
        public IRef<PostmanCollection> postmanCollectionRef;

        public string version => "v2.1";

        #endregion

        [EastFive.Api.HttpGet]
        public static IHttpResponse GetSchema(
                [QueryParameter] string flow,
                [OptionalQueryParameter]string collections,
                //Security security,
                IInvokeApplication invokeApplication,
                HttpApplication httpApp, IHttpRequest request, IProvideUrl url,
            ContentTypeResponse<Resources.Collection.Collection> onSuccess,
            NotFoundResponse onNotFound)
        {
            var lookups = httpApp
                .GetResources()
                .Where(
                    resource =>
                    {
                        if (collections.IsNullOrWhiteSpace())
                            return true;
                        var collection = resource.TryGetAttributeInterface(
                                out EastFive.Api.Meta.OpenApi.IDocumentOpenApiRoute documentOpenApiRoute) ?
                            documentOpenApiRoute.Collection
                            :
                            resource.Namespace;
                        return collection.StartsWith(collections, StringComparison.OrdinalIgnoreCase);
                    })
                .ToArray();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);

            return manifest.Routes
                .SelectMany(route => route.Methods)
                .TryWhere(
                    (Api.Resources.Method method, out IDefineFlow flowAttr) =>
                        method.MethodPoco.TryGetAttributeInterface(out flowAttr))
                .GroupBy(methodAndFlow => methodAndFlow.@out.FlowName)
                .Where(grp => grp.Key.Equals(flow, StringComparison.OrdinalIgnoreCase))
                .First(
                    (methodAndFlowGrp, next) =>
                    {
                        var info = new Resources.Collection.Info
                        {
                            _postman_id = Guid.NewGuid(),
                            name = methodAndFlowGrp.Key,
                            schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
                        };

                        var items = methodAndFlowGrp
                            .OrderBy(methodAndFlow => methodAndFlow.@out.Step)
                            .Select(
                                methodAndFlow => methodAndFlow.@out.GetItem(methodAndFlow.item))
                            .ToArray();

                        var collection = new Resources.Collection.Collection()
                        {
                            info = info,
                            item = items,
                        };
                        return onSuccess(collection);
                    },
                    () => onNotFound());
        }
    }


}
