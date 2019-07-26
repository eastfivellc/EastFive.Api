using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    // TODO: Change this to IDocumentRoute 
    // and add IDocumentProperty, IDocumentMethod, IDocumentParameter, IDocumentResponse, etc

    public interface IProvideDocumentation
    {
        Resources.Route GetRoute(Type type, HttpApplication httpApp);
    }
}
