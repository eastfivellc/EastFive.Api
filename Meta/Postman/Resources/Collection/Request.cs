using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Request
    {
        public string method { get; set; }
        public Header[] header { get; set; }
        public Url url { get; set; }
        public Body body { get; set; }
        public string description { get; set; }
    }

    public class Url
    {
        public string raw { get; set; }
        public string[] host { get; set; }
        public string[] path { get; set; }
        public QueryItem[] query { get; set; }
    }

    public class Header
    {
        public string key { get; set; }
        public string value { get; set; }
        public string type { get; set; }
    }

    public class QueryItem
    {
        public string key { get; set; }
        public string value { get; set; }
    }

}
