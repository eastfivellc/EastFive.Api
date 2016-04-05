using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests
{
    public class MockPrincipal : System.Security.Principal.IPrincipal
    {
        public MockPrincipal(string userId)
        {
            if (default(string) == userId)
                userId = Guid.NewGuid().ToString();
            var identity = new GenericIdentity(userId);

            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            identity.AddClaim(new Claim(ClaimTypes.Name, userId));
            this.Identity = identity;
        }

        private class Identity_ : IIdentity
        {
            public Identity_()
            {
                this.Name = Guid.NewGuid().ToString();
            }

            public string AuthenticationType
            {
                get
                {
                    return "MockAuthenticationForTesting";
                }
            }

            public bool IsAuthenticated
            {
                get
                {
                    return true;
                }
            }

            public string Name { get; private set; }
        }
        
        public IIdentity Identity { get; private set; }
        public TestSession Session { get; internal set; }
        public Guid Id { get; internal set; }

        public bool IsInRole(string role)
        {
            return true;
        }
    }
}
