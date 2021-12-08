using System;
namespace EastFive.Api.Meta.Postman.Resources
{
    public class CollectionSummary
    {
        public string id;
        public string name;
        public string owner;
        public DateTime createdAt;
        public DateTime updatedAt;
        public Guid uid;
        public bool isPulic;
    }

    public class CollectionSummaries
    {
        public CollectionSummary[] collections;
    }
}

