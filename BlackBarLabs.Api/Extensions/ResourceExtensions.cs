using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http.Routing;

using BlackBarLabs.Api.Extensions;
using BlackBarLabs.Core.Extensions;
using BlackBarLabs.Web;
using BlackBarLabs.Web.Services;

namespace BlackBarLabs.Api
{
    public static class ResourceExtensions
    {
        public static IEnumerable<Guid> ParseGuidString(this string guidString)
        {
            if (String.IsNullOrWhiteSpace(guidString))
                return new Guid[] { };

            var guids = guidString.Split(new char[','])
                .Where(guidStringCandidate => { Guid g; return Guid.TryParse(guidStringCandidate, out g); })
                .Select(guidStringCandidate => { Guid g; Guid.TryParse(guidStringCandidate, out g); return g; });
            return guids;
        }

        public static TResult ParseGuidString<TResult>(this string guidString,
            Func<IEnumerable<Guid>, TResult> multiple,
            Func<TResult> none)
        {
            if (String.IsNullOrWhiteSpace(guidString))
                return none();

            var guids = guidString.Split(new char[] { ',' })
                .Where(guidStringCandidate =>
                {
                    Guid g;
                    var validGuid = Guid.TryParse(guidStringCandidate, out g);
                    return validGuid;
                })
                .Select(guidStringCandidate => { Guid g; Guid.TryParse(guidStringCandidate, out g); return g; })
                .ToArray();
            return multiple(guids);
        }

        public static Resources.WebId GetWebId<TController>(this UrlHelper url,
            Guid id,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Id = id });
            return new Resources.WebId
            {
                Key = id.ToString(),
                UUID = id,
                URN = id.ToWebUrn(controllerName, ""),
                Source = new Uri(location),
            };
        }
        
        public static IEnumerable<System.Security.Claims.Claim> GetClaims(this HttpRequestMessage request)
        {
            if (request.IsDefaultOrNull())
                yield break;
            if (request.Headers.IsDefaultOrNull())
                yield break;
            var claimsContext = request.Headers.Authorization.GetClaimsFromAuthorizationHeader();
            if (claimsContext.IsDefaultOrNull())
                yield break;
            foreach (var claim in claimsContext)
                yield return claim;
        }
        
        public static Func<ISendMailService> GetMailService(this HttpRequestMessage request)
        {
            var mailService = default(ISendMailService);
            return () =>
            {
                if (mailService.IsDefaultOrNull())
                {
                    var getMailService = (Func<ISendMailService>)
                        request.Properties[ServicePropertyDefinitions.MailService];
                    mailService = getMailService();
                }
                return mailService;
            };
        }
        
        public static Func<ITimeService> GetDateTimeService(this HttpRequestMessage request)
        {
            var dateTimeService = default(ITimeService);
            return () =>
            {
                if (dateTimeService.IsDefaultOrNull())
                {
                    if (!request.Properties.ContainsKey(ServicePropertyDefinitions.TimeService))
                        dateTimeService = new Services.TimeService();
                    else
                        dateTimeService = ((Func<ITimeService>)
                            request.Properties[ServicePropertyDefinitions.TimeService])();
                }
                return dateTimeService;
            };
        }

        public static string ToStringOneCharacter(this DayOfWeek dayOfWeek)
        {
            var dtInfo = new System.Globalization.DateTimeFormatInfo();
            dtInfo.AbbreviatedDayNames = new string[] { "U", "M", "T", "W", "R", "F", "S" }; // MTWRFSU
            var dayOfWeekString = dtInfo.GetDayName(dayOfWeek);
            return dayOfWeekString;
        }

        public static TResult ToDayOfWeek<TResult>(this string oneCharacterDayOfWeekAsString,
            Func<DayOfWeek, TResult> success,
            Func<TResult> noMatch)
        {
            var mapping = new Dictionary<string, DayOfWeek>()
            {
                { "U", DayOfWeek.Sunday },
                { "M", DayOfWeek.Monday },
                { "T", DayOfWeek.Tuesday },
                { "W", DayOfWeek.Wednesday },
                { "R", DayOfWeek.Thursday },
                { "F", DayOfWeek.Friday },
                { "S", DayOfWeek.Saturday },
            };
            if (mapping.ContainsKey(oneCharacterDayOfWeekAsString.ToUpper()))
                return success(mapping[oneCharacterDayOfWeekAsString.ToUpper()]);
            DayOfWeek dayOfWeek;
            if (Enum.TryParse(oneCharacterDayOfWeekAsString, out dayOfWeek))
                return success(dayOfWeek);
            return noMatch();
        }
        
        public static bool IsEmpty(this Resources.WebId webId)
        {
            return
                webId.IsDefaultOrNull() ||
                (
                    String.IsNullOrWhiteSpace(webId.Key) &&
                    webId.UUID.IsDefaultOrEmpty() &&
                    webId.URN.IsDefault() &&
                    webId.Source.IsDefault()
                );
        }

        public static TResult GetUUID<TResult>(this Resources.WebId webId,
            Func<Guid, TResult> success,
            Func<TResult> isEmpty)
        {
            if (webId.IsEmpty())
                return isEmpty();
            if (webId.UUID.IsDefaultOrEmpty())
                return isEmpty();
            return success(webId.UUID);
        }
    }
}
