﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Net.Http;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api.Resources
{

    [FunctionViewController6(Route = "ManifestRoute")]
    public class Route
    {
        [EastFive.Api.HttpGet]
        public static HttpResponseMessage FindAsync(
                HttpApplication httpApp, HttpRequestMessage request,
            ContentTypeResponse<Route[]> onContent)
        {
            var lookups = httpApp.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);
            return onContent(manifest.Routes);
        }

        public Route(string name, KeyValuePair<HttpMethod, MethodInfo[]>[] methods,
            HttpApplication httpApp)
        {
            this.Name = name;
            this.Methods = methods
                .SelectMany(kvp => kvp.Value.Select(m => m.PairWithKey(kvp.Key)))
                .Select(verb => new Method(verb.Key.Method, verb.Value, httpApp)).ToArray();
            this.Properties = methods
                .First(
                    (methodKvp, next) =>
                    {
                        return methodKvp.Value
                            .First(
                                (method, nextInner) =>
                                {
                                    return method.DeclaringType
                                        .GetPropertyOrFieldMembers()
                                        .Where(property => property.ContainsCustomAttribute<JsonPropertyAttribute>())
                                        .Select(member => new Property(member, httpApp))
                                        .ToArray();
                                    //return new Property[] { };
                                },
                                () => new Property[] { });
                    },
                    () => new Property[] { });
        }

        public Route(string name, MethodInfo[] methods, MemberInfo[] properties,
            HttpApplication httpApp)
        {
            this.Name = name;
            this.Methods = methods
                .Where(method => method.ContainsAttributeInterface<IDocumentMethod>())
                .Select(
                    method =>
                    {
                        var docMethod = method.GetAttributesInterface<IDocumentMethod>().First();
                        return docMethod.GetMethod(method, httpApp);
                    })
                .ToArray();
            this.Properties = properties
                .Where(method => method.ContainsAttributeInterface<IDocumentProperty>())
                .Select(method => method.GetAttributesInterface<IDocumentProperty>()
                    .First()
                    .GetProperty(method, httpApp))
                .ToArray();
        }

        public string Name { get; set; }

        public Method[] Methods { get; set; }

        public Property[] Properties { get; set; }
    }
}
