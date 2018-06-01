using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewControllerAttribute : Attribute
    {
        public string Route { get; set; }
    }
}
