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

        [EastFive.Api.HttpOptions]
        public static IHttpResponse ListFlows(
                //Security security,
                IInvokeApplication invokeApplication,
                HttpApplication httpApp, IHttpRequest request, IProvideUrl url,
            ContentTypeResponse<string []> onSuccess,
            NotFoundResponse onNotFound)
        {
            var lookups = httpApp
                .GetResources()
                .ToArray();

            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);

            var flows = manifest.Routes
                .SelectMany(route => route.Methods)
                .SelectMany(method => method.MethodPoco
                    .GetAttributesInterface<IDefineFlow>(multiple: true)
                    .Select(attr => (method, attr)))
                .GroupBy(methodAndFlow => methodAndFlow.attr.FlowName)
                .Where(grp => grp.Key.HasBlackSpace())
                .Select(grp => grp.Key)
                .ToArray();

            return onSuccess(flows);
        }

        [EastFive.Api.HttpGet]
        public static IHttpResponse GetSchema(
                [QueryParameter] string flow,
                [OptionalQueryParameter]string collections,
                [OptionalQueryParameter]bool? preferJson,
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
                .SelectMany(method => method.MethodPoco
                    .GetAttributesInterface<IDefineFlow>(multiple:true)
                    .Select(attr => (method, attr)))
                .GroupBy(methodAndFlow => methodAndFlow.attr.FlowName)
                .Where(grp => grp.Key.HasBlackSpace())
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

                        var customItems = manifest.Routes
                            .SelectMany(route => route.Type.GetAttributesInterface<IDefineFlow>(multiple: true))
                            .Select(flowAttr => (flowAttr, flowAttr.GetItem(default(Api.Resources.Method),
                                preferJson.HasValue? preferJson.Value : false)))
                            .Where(grp => grp.Item1.FlowName.Equals(flow, StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        var items = methodAndFlowGrp
                            .Select(
                                methodAndFlow => (methodAndFlow.attr, methodAndFlow.attr.GetItem(methodAndFlow.method,
                                    preferJson.HasValue ? preferJson.Value : false)))
                            .Concat(customItems)
                            .OrderBy(methodAndFlow => methodAndFlow.Item1.Step)
                            .Select(tpl => tpl.Item2)
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
