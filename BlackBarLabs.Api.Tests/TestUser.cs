using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests
{
    public class TestUser : System.Security.Principal.IPrincipal
    {
        public static Task<T> StartAsync<T>(Func<TestSession, TestUser, Task<T>> callback)
        {
            var session = new TestSession();
            var result = session.WithUserAsync(Guid.NewGuid(),
                async (user) =>
                {
                    return await callback(session, user);
                });
            return result;
        }

        public static Task StartAsync(Func<TestSession, TestUser, Task> callback)
        {
            var session = new TestSession();
            var result = session.WithUserAsync(Guid.NewGuid(),
                async (user) =>
                {
                    await callback(session, user);
                    return true;
                });
            return result;
        }

        public TestUser(TestSession session, Guid userId = default(Guid))
        {
            if (default(Guid) == userId)
                userId = Guid.NewGuid();
            this.Id = userId;
            var userIdString = userId.ToString();
            var identity = new GenericIdentity(userIdString);

            this.Session = session;

            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdString));
            identity.AddClaim(new Claim(ClaimTypes.Name, userIdString));
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
