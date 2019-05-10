using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IBuildUrls
    {
        IQueryable<T> Resources<T>();
    }

    public interface IRenderUrls
    {
        Uri RenderLocation(string routeName = "DefaultApi");
    }
}
