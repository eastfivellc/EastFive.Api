using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public static class HttpHeaderExtensions
    {
        public static CultureInfo[] ToCultures(this HttpHeaderValueCollection<StringWithQualityHeaderValue> acceptsCultures)
        {
            var acceptLookup = acceptsCultures
                .NullToEmpty()
                .Select(acceptHeader => acceptHeader.Value.ToLowerInvariant().PairWithValue(acceptHeader.Quality))
                .ToDictionary();
            return CultureInfo.GetCultures(CultureTypes.AllCultures)
                .OrderBy(
                    culture =>
                    {
                        var lookupKey = culture.Name.ToLowerInvariant();
                        if (!acceptLookup.ContainsKey(lookupKey))
                            return -1.0;
                        var valueMaybe = acceptLookup[lookupKey];
                        if (!valueMaybe.HasValue)
                            return -1.0;
                        return valueMaybe.Value;
                    })
                .ToArray();
        }
    }
}
