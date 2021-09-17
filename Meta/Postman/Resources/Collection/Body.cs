﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Body
    {
        public string mode { get; set; }
        public string raw { get; set; }
        public Options options { get; set; }
    }


    public class Options
    {
        public Raw raw { get; set; }
    }

    public class Raw
    {
        public string language { get; set; }
    }


}