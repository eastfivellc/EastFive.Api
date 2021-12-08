﻿using System;
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

        public static Task<TResult> GetAsync<TResult>(IRef<Collection> collectionRef,
            Func<Collection, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections/{collectionRef.id}", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientGetResourceAsync(
                        (Collection collection) =>
                        {
                            return onFound(collection);
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
                });
        }

        public Task<TResult> CreateAsync<TResult>(
            Func<Collection, TResult> onFound)
        {
            var collection = new CollectionCollection() { collection = this };
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientPostResourceAsync(collection,
                        (CollectionCollection collectionUpdated) =>
                        {
                            return onFound(collectionUpdated.collection);
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
            Func<Collection, TResult> onFound)
        {
            var collection = this;
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections/{collection.id}", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientPutResourceAsync(collection,
                        (Collection collectionUpdated) =>
                        {
                            return onFound(collectionUpdated);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        });
                });
        }
    }
}
