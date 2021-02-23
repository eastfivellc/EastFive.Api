using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EastFive.Api.Meta.OpenApi
{
    [FunctionViewController(Route = "PostmanLink")]
    public class OpenApiSchema : IReferenceable
    {
        #region Properties

        [JsonIgnore]
        public Guid id => this.openApiSchemaRef.id;

        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [ApiProperty(PropertyName = IdPropertyName)]
        public IRef<OpenApiSchema> openApiSchemaRef;

        public string openapi => "3.0.0";

        public Info info;

        public Server[] servers { get; set; }
        public Paths paths { get; set; }
        public Components components { get; set; }

        #endregion

        //[EastFive.Api.HttpGet]
        //public static IHttpResponse List(
        //        //Security security,
        //        HttpApplication application, IHttpRequest request, IProvideUrl url,
        //    NoContentResponse onSuccess,
        //    ViewFileResponse<Api.Resources.Manifest> onHtml)
        //{
        //    application
        //        .GetResources()
        //         ;
        //}
    }


    public class Info
    {
        public string version { get; set; }
        public string title { get; set; }
        public License license { get; set; }
    }

    public class License
    {
        public string name { get; set; }
    }

    public class Paths
    {
        public Pets pets { get; set; }
        public PetsPetid petspetId { get; set; }
    }

    public class Pets
    {
        public Get get { get; set; }
        public Post post { get; set; }
    }

    //public class Pets
    //{
    //    public string type { get; set; }
    //    public Items items { get; set; }
    //}

    public class Get
    {
        public string summary { get; set; }
        public string operationId { get; set; }
        public string[] tags { get; set; }
        public Parameter[] parameters { get; set; }
        public Responses responses { get; set; }
    }

    public class Responses
    {
        public _200 _200 { get; set; }
        public Default _default { get; set; }
    }

    public class _200
    {
        public string description { get; set; }
        public Headers headers { get; set; }
        public Content content { get; set; }
    }

    public class Headers
    {
        public XNext xnext { get; set; }
    }

    public class XNext
    {
        public string description { get; set; }
        public Schema schema { get; set; }
    }

    public class Schema
    {
        public string type { get; set; }
    }

    public class Content
    {
        public ApplicationJson applicationjson { get; set; }
    }

    public class ApplicationJson
    {
        public Schema1 schema { get; set; }
    }

    public class Schema1
    {
        public string _ref { get; set; }
    }

    public class Default
    {
        public string description { get; set; }
        public Content1 content { get; set; }
    }

    public class Content1
    {
        public ApplicationJson1 applicationjson { get; set; }
    }

    public class ApplicationJson1
    {
        public Schema2 schema { get; set; }
    }

    public class Schema2
    {
        public string _ref { get; set; }
    }

    public class Parameter
    {
        public string name { get; set; }
        public string _in { get; set; }
        public string description { get; set; }
        public bool required { get; set; }
        public Schema3 schema { get; set; }
    }

    public class Schema3
    {
        public string type { get; set; }
        public string format { get; set; }
    }

    public class Post
    {
        public string summary { get; set; }
        public string operationId { get; set; }
        public string[] tags { get; set; }
        public Responses1 responses { get; set; }
    }

    public class Responses1
    {
        public _201 _201 { get; set; }
        public Default1 _default { get; set; }
    }

    public class _201
    {
        public string description { get; set; }
    }

    public class Default1
    {
        public string description { get; set; }
        public Content2 content { get; set; }
    }

    public class Content2
    {
        public ApplicationJson2 applicationjson { get; set; }
    }

    public class ApplicationJson2
    {
        public Schema4 schema { get; set; }
    }

    public class Schema4
    {
        public string _ref { get; set; }
    }

    public class PetsPetid
    {
        public Get1 get { get; set; }
    }

    public class Get1
    {
        public string summary { get; set; }
        public string operationId { get; set; }
        public string[] tags { get; set; }
        public Parameter1[] parameters { get; set; }
        public Responses2 responses { get; set; }
    }

    public class Responses2
    {
        public _2001 _200 { get; set; }
        public Default2 _default { get; set; }
    }

    public class _2001
    {
        public string description { get; set; }
        public Content3 content { get; set; }
    }

    public class Content3
    {
        public ApplicationJson3 applicationjson { get; set; }
    }

    public class ApplicationJson3
    {
        public Schema5 schema { get; set; }
    }

    public class Schema5
    {
        public string _ref { get; set; }
    }

    public class Default2
    {
        public string description { get; set; }
        public Content4 content { get; set; }
    }

    public class Content4
    {
        public ApplicationJson4 applicationjson { get; set; }
    }

    public class ApplicationJson4
    {
        public Schema6 schema { get; set; }
    }

    public class Schema6
    {
        public string _ref { get; set; }
    }

    public class Parameter1
    {
        public string name { get; set; }
        public string _in { get; set; }
        public bool required { get; set; }
        public string description { get; set; }
        public Schema7 schema { get; set; }
    }

    public class Schema7
    {
        public string type { get; set; }
    }

    public class Components
    {
        public Schemas schemas { get; set; }
    }

    public class Schemas
    {
        public Pet Pet { get; set; }
        public Pets Pets { get; set; }
        public Error Error { get; set; }
    }

    public class Pet
    {
        public string type { get; set; }
        public string[] required { get; set; }
        public Properties properties { get; set; }
    }

    public class Properties
    {
        public Id id { get; set; }
        public Name name { get; set; }
        public Tag tag { get; set; }
    }

    public class Id
    {
        public string type { get; set; }
        public string format { get; set; }
    }

    public class Name
    {
        public string type { get; set; }
    }

    public class Tag
    {
        public string type { get; set; }
    }

    public class Items
    {
        public string _ref { get; set; }
    }

    public class Error
    {
        public string type { get; set; }
        public string[] required { get; set; }
        public Properties1 properties { get; set; }
    }

    public class Properties1
    {
        public Code code { get; set; }
        public Message message { get; set; }
    }

    public class Code
    {
        public string type { get; set; }
        public string format { get; set; }
    }

    public class Message
    {
        public string type { get; set; }
    }

    public class Server
    {
        public string url { get; set; }
    }

}
