using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{


    public class Collection : IReferenceable
    {
        public Guid id => info._postman_id;

        public Info info { get; set; }
        public Item[] item { get; set; }


        public static async Task<Collection> GetCollection(IRef<Collection> collectionRef)
        {
            return new Collection()
            {

            };
        }
    }
}
