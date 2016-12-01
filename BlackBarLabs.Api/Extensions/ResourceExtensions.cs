using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http.Routing;

using BlackBarLabs.Api.Extensions;
using BlackBarLabs.Core.Extensions;
using BlackBarLabs.Web;
using BlackBarLabs.Web.Services;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;

using BlackBarLabs.Core;
using BlackBarLabs.Collections.Generic;

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

        public static Resources.WebId GetWebId<TController>(this UrlHelper url,
            string urn,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Id = default(Guid) });
            return new Resources.WebId
            {
                Key = default(Guid).ToString(),
                UUID = default(Guid),
                URN = new Uri(urn),
                Source = new Uri(location)
            };
        }

        public static Resources.WebId GetWebId(this UrlHelper url,
            Type controllerType,
            string urnNamespace,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });

            return new Resources.WebId
            {
                Key = string.Empty,
                UUID = Guid.Empty,
                URN = controllerType.GetUrn(urnNamespace),
                Source = new Uri(location),
            };
        }

        public static Uri GetUrn(this Type controllerType,
            string urnNamespace)
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);

            var urn = new Uri("urn:" + urnNamespace + ":" + controllerName);
            var resourceAttributeTypes = controllerType.GetCustomAttributes<Api.ResourceTypeAttribute>();
            if (resourceAttributeTypes.Length > 0)
            {
                var urnModelType = resourceAttributeTypes[0].Urn;
                var modelAttributeTypes = controllerType.GetCustomAttributes<Web.ResourceTypeAttribute>();
                if (modelAttributeTypes.Length > 0)
                {
                    urn = new Uri(modelAttributeTypes[0].Urn);
                }
            }
            return urn;
        }

        public static Uri GetLocation(this UrlHelper url, Type controllerType,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });
            return new Uri(location);
        }

        public static Uri GetLocation<TController>(this UrlHelper url,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });
            return new Uri(location);
        }

        public static Uri GetLocation<TController>(this UrlHelper url,
            Guid id,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Id = id });
            return new Uri(location);
        }
        
        public static Uri GetLocation<TController>(this UrlHelper url,
            string action,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Action = action });
            return new Uri(location);
        }

        public static Uri GetLocationWithQuery(this UrlHelper url, Type controllerType, string query,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });
            query = query.StartsWith($"?") ? query.Substring(1) : query;
            var uri = new UriBuilder(location) {Query = query};
            return uri.Uri;
        }

        public static Uri GetLocationWithId(this UrlHelper url, Type controllerType, Guid id,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });
            location = location + "/" + id;
            return new Uri(location);
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

        public static IEnumerable<System.Security.Claims.Claim> GetClaims(this HttpRequestBase request)
        {
            if (request.IsDefaultOrNull())
                yield break;
            if (request.Headers.IsDefaultOrNull())
                yield break;
            var authorizationString = request.Headers["Authorization"];
            if (authorizationString.IsDefaultOrNull())
                yield break;
            var authenticationHeaderValue = AuthenticationHeaderValue.Parse(authorizationString);
            var claimsContext = authenticationHeaderValue.GetClaimsFromAuthorizationHeader();
            if (claimsContext.IsDefaultOrNull())
                yield break;
            foreach (var claim in claimsContext)
                yield return claim;
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
        
        public static Resources.WebId GetWebIdUUID(this Guid uuId)
        {
            return new Resources.WebId() { UUID = uuId };
        }
        
        public static Guid[] ToGuids(this WebId[] webIds)
        {
            var guids = webIds
                .NullToEmpty()
                .Select(wId => wId.UUID)
                .ToArray();
            return guids;
        }

        public static WebId[] ToWebIds<TController>(this Guid[] guids, UrlHelper url)
        {
            var webIds = guids
                .NullToEmpty()
                .Select(guid => url.GetWebId<TController>(guid))
                .ToArray();
            return webIds;
        }
        
    }
}
