using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IMutateQuery
    {
        UriBuilder GetQueryParameters(UriBuilder urlBuilder, MethodCallExpression expr);
    }
}
