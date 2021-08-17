using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Collection
    {
        public Info info { get; set; }
        public Item[] item { get; set; }
    }
}
