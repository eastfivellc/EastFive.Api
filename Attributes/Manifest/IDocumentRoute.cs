using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    // TODO: add IDocumentProperty, IDocumentMethod, IDocumentParameter

    public interface IDocumentRoute
    {
        Resources.Route GetRoute(Type type, HttpApplication httpApp);
    }
}
