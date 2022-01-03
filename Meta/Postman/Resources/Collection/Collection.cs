using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Net;
using EastFive.Web.Configuration;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Collection : IReferenceable
    {
        public Guid id => info._postman_id;

        public Info info { get; set; }
        public Item[] item { get; set; }
        public Variable[] variable { get; set; }

        public static Task<TResult> GetAsync<TResult>(string collectionId,
            Func<Collection, TResult> onFound,
            Func<TResult> onNotFound,
            Func<string, TResult> onFailure = default)
        {
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections/{collectionId}", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientGetResourceAsync(
                        (CollectionCollection collection) =>
                        {
                            return onFound(collection.collection);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        },
                        onFailureWithBody:(statusCode, body) =>
                        {
                            if (statusCode == System.Net.HttpStatusCode.NotFound)
                                return onNotFound();
                            throw new Exception(body);
                        });
                },
                onUnspecified:onFailure.AsAsyncFunc());
        }

        public Task<TResult> CreateAsync<TResult>(
            Func<CollectionSummary, TResult> onCreated)
        {
            var collection = new CollectionCollection() { collection = this };
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientPostResourceAsync(collection,
                        (CollectionSummaryParent collectionUpdated) =>
                        {
                            return onCreated(collectionUpdated.collection);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        });
                });
        }

        public class CollectionCollection
        {
            public Collection collection;
        }


        public Task<TResult> UpdateAsync<TResult>(
            Func<Collection, TResult> onFound,
            Func<string, TResult> onFailure)
        {
            var collection = new CollectionCollection() { collection = this };
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections/{collection.collection.id}", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientPutResourceAsync(collection,
                        (Collection collectionUpdated) =>
                        {
                            return onFound(collectionUpdated);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        },
                        onFailureWithBody:(statusCode, body) => onFailure(body));
                });
        }
    }
}
