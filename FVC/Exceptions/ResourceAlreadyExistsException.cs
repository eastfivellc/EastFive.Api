using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ResourceAlreadyExists : ResourceConflictException, IHttpResponseMessageException
    {
        private object currentOrderId;

        public ResourceAlreadyExists(object currentOrderId)
        {
            this.currentOrderId = currentOrderId;
        }
    }
}
