using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization;
using System.Collections;

namespace EastFive.Api.Serialization.Json
{
    public class CastSerializationAttribute : Attribute, IProvideSerialization
    {
        public CastSerializationAttribute()
        {
        }

        public string MediaType => "application/json";

        public string ContentType { get; set; }

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public bool IgnoreNull { get; set; } = true;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            return 0.1;
        }

        public async Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request,
            ParameterInfo paramInfo, object obj)
        {
            var jsonCastersHttpApp = httpApp
                .GetType()
                .GetAttributesInterface<ICastJson>()
                .ToArray();

            using (var textWriter = request.TryGetAcceptCharset(out Encoding writerEncoding) ?
                new StreamWriter(responseStream, writerEncoding)
                :
                new StreamWriter(responseStream, new UTF8Encoding(false)))
            {
                var jsonWriter = new JsonTextWriter(textWriter);

                var serializer = new JsonSerializer();
                var converter = new Serialization.ExtrudeConvert(request, httpApp);
                serializer.Converters.Add(converter);

                await WriteToStreamAsync(jsonWriter, serializer, obj);
            }

            async Task WriteToStreamAsync(JsonTextWriter jsonWriter, JsonSerializer serializer, object obj)
            {
                if (obj.IsNull())
                {
                    await jsonWriter.WriteNullAsync();
                    return;
                }

                await jsonWriter.WriteStartObjectAsync();

                var members = obj.GetType()
                    .GetPropertyAndFieldsWithAttributesInterface<IProvideApiValue>();
                foreach (var (member, apiValueProvider) in members)
                {
                    var memberValue = member.GetPropertyOrFieldValue(obj);
                    if (memberValue.IsNull() && IgnoreNull)
                        continue;
                    await WriteAsync(member.GetAttributesInterface<ICastJson>(),
                        onCouldNotCast: () =>
                        {
                            var objType = obj.GetType();
                            return WriteAsync(objType.GetAttributesInterface<ICastJson>(),
                                () => WriteAsync(jsonCastersHttpApp,
                                    () =>
                                    {
                                        var memberType = member.GetPropertyOrFieldType();
                                        return WriteAsync(memberType.GetAttributesInterface<ICastJson>(),
                                           async () =>
                                           {
                                               if (memberType.IsArray)
                                               {
                                                   await jsonWriter.WritePropertyNameAsync(apiValueProvider.PropertyName);
                                                   await WriteArrayAsync(memberType);
                                                   return;
                                               }
                                               await jsonWriter.WriteCommentAsync(
                                                   $"Cannot find {nameof(ICastJson)} interface for " +
                                                   $"{member.DeclaringType.FullName}..{member.Name}");
                                               return;
                                           });
                                    }));
                        });

                    async Task WriteArrayAsync(Type memberType)
                    {
                        if(memberValue.IsNull())
                        {
                            await jsonWriter.WriteNullAsync();
                            return;
                        }
                        var enumerable = (IEnumerable)memberValue;
                        await jsonWriter.WriteStartArrayAsync();

                        if (!enumerable.IsNull())
                        {
                            foreach (var item in enumerable)
                            {
                                await WriteToStreamAsync(jsonWriter, serializer, item);
                            }
                        }
                        await jsonWriter.WriteEndArrayAsync();
                    }

                    Task WriteAsync(IEnumerable<ICastJson> castAttrs,
                        Func<Task> onCouldNotCast)
                    {
                        return castAttrs
                            .Where(attr => attr.CanConvert(member, paramInfo,
                                                httpRequest: request, application: httpApp,
                                                apiValueProvider: apiValueProvider, objectValue: obj))
                            .First(
                                async (jsonCaster, next) =>
                                {
                                    var memberValue = member.GetPropertyOrFieldValue(obj);
                                    var isNull = memberValue.IsNull();
                                    if (isNull)
                                        if (IgnoreNull)
                                            return;

                                    await jsonWriter.WritePropertyNameAsync(apiValueProvider.PropertyName);
                                    await jsonCaster.WriteAsync(jsonWriter, serializer, member, paramInfo,
                                        apiValueProvider, obj, memberValue, request, httpApp);
                                },
                                () => onCouldNotCast());
                    }
                }

                await jsonWriter.WriteEndObjectAsync();
            }
        }
    }
}

