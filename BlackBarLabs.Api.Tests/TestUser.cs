using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using BlackBarLabs.Core.Collections;

namespace BlackBarLabs.Api.Tests
{
    public class TestUser : IPrincipal
    {
        public async static Task StartAsync(Func<TestSession, TestUser, Task> callback)
        {
            var session = new TestSession();
            await session.WithUserAsync(Guid.NewGuid(),
                async (user) =>
                {
                    await callback(session, user);
                });
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

        public void AddClaim(string type, string value)
        {
            ((ClaimsIdentity)Identity).AddClaim(new Claim(type, value));
        }

        public void UpdateAuthorizationToken()
        {
            //TODO Add FetchClaims extension method in OrderOwl to actually get claims from Claims endpoint instead of off of user

            this.Session.Headers.AddOrReplace("Authorization", "Bearer " + Security.Tokens.JwtTools.CreateToken(
                Session.Id.ToString(), DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow + TimeSpan.FromMinutes(60),
                ((ClaimsIdentity) Identity).Claims,
                "AuthServer.issuer", "AuthServer.publicKey"));
        }
    }
}
