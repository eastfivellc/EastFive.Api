using System;
using System.Collections.Generic;
using System.Text;

namespace EastFive.Api
{
    public interface IProvideUrl
    {
        Uri Link(string routeName, string controllerName, string action = default, string id = default);
    }
}
