using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IInvokeApplicationFactory
    {
        IInvokeApplication GetUnauthorizedSession();

        IInvokeApplication GetAuthorizedSession(string token);
    }
}
