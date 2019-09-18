using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IFilterApiValues
    {
        HttpRequestMessage MutateRequest(HttpRequestMessage request, HttpMethod httpMethod, 
            MethodInfo method, Expression[] arguments);

        Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments);
    }
}
