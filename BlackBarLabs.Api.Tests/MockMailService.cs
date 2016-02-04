using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests
{
    public class MockMailService : BlackBarLabs.Web.ISendMailService
    {
        public delegate Task SendEmailMessageDelegate(string toAddress, string fromAddress,
            string fromName, string subject, string html, IDictionary<string, List<string>> substitution);

        public SendEmailMessageDelegate SendEmailMessageCallback { get; set; }

        public Task SendEmailMessageAsync(string toAddress, string fromAddress,
            string fromName, string subject, string html, IDictionary<string, List<string>> substitution = null)
        {
            return this.SendEmailMessageCallback.Invoke(
                toAddress, fromAddress, fromName, subject, html, substitution);
        }
    }
}
