﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Routing;

using Newtonsoft.Json;

using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Extensions;
using System.Reflection;
using EastFive.Linq.Async;

namespace EastFive.Api.Serialization
{
    public class ExtrudeConvert : Newtonsoft.Json.JsonConverter
    {
        IApplication application;
        IHttpRequest request;
        IConvertJson[] jsonConverters;

        public ExtrudeConvert(IHttpRequest request, IApplication application)
        {
            this.request = request;
            this.application = application;
            this.jsonConverters = application.GetType()
                .GetAttributesInterface<IConvertJson>()
                .ToArray();
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                var shouldConvertDict = objectType
                    .GetGenericArguments()
                    .Any(ShouldConvertDictionaryType);
                if (shouldConvertDict)
                    return true;
            }
            if (objectType.IsSubclassOf(typeof(Type)))
                return true;
            if (objectType.IsEnum)
                return true;
            return jsonConverters.Any(jc => jc.CanConvert(objectType, this.request, this.application));
        }

        protected bool ShouldConvertDictionaryType(Type arg)
        {
            if (CanConvert(arg))
                return true;
            if (arg.ContainsCustomAttribute<IProvideSerialization>(true))
                return true;
            if (arg == typeof(object))
                return true;
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Extruder does not read values");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var converters = jsonConverters
                .Where(jc => jc.CanConvert(value.GetType(), this.request, this.application));
            if(converters.Any())
            {
                converters.First().Write(writer, value, serializer,
                    this.request, this.application);
                return;
            }

            void WriteId(Guid? idMaybe)
            {
                if (!idMaybe.HasValue)
                {
                    writer.WriteValue((string)null);
                    return;
                }

                var id = idMaybe.Value;
                writer.WriteValue(id);
            }

            if (value is IReferenceable)
            {
                var id = (value as IReferenceable).id;
                WriteId(id);
                //writer.WriteValue(id);
                return;
            }

            if (value is IReferences)
            {
                writer.WriteStartArray();
                Guid[] ids = (value as IReferences).ids
                    .Select(
                        id =>
                        {
                            WriteId(id);
                                //writer.WriteValue(id);
                                return id;
                        })
                    .ToArray();
                writer.WriteEndArray();
                return;
            }

            if (value is IReferenceableOptional)
            {
                var id = (value as IReferenceableOptional).id;
                WriteId(id);
                //writer.WriteValue(id);
                return;
            }

            if(!value.TryGetType(out Type valueType))
            {
                writer.WriteNull();
                return;
            }

            if (valueType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                writer.WriteStartObject();
                foreach (var kvpObj in value.DictionaryKeyValuePairs())
                {
                    var keyValue = kvpObj.Key;
                    var propertyName = (keyValue is IReferenceable) ?
                        (keyValue as IReferenceable).id.ToString()
                        :
                        keyValue.ToString();
                    writer.WritePropertyName(propertyName);

                    var valueValue = kvpObj.Value;
                    var valueValueType = valueType.GenericTypeArguments.Last();
                    if (this.ShouldConvertDictionaryType(valueValueType))
                    {
                        WriteJson(writer, valueValue, serializer);
                        continue;
                    }
                    writer.WriteValue(valueValue);
                }
                writer.WriteEndObject();
                return;
            }

            if (value is Type)
            {
                var typeValue = (value as Type);
                var serializationAttrs = typeValue
                    .GetAttributesInterface<IProvideSerialization>();
                if(serializationAttrs.Any())
                {
                    var serializationAttr = serializationAttrs
                            .OrderByDescending(x => x.GetPreference(request))
                            .First();
                    writer.WriteValue(serializationAttr.ContentType);
                    return;
                }
                var stringType = typeValue.GetClrString();
                writer.WriteValue(stringType);
                return;
            }

            if(valueType.IsEnum)
            {
                var stringValue = Enum.GetName(valueType, value);
                writer.WriteValue(stringValue);
                return;
            }

            serializer.Serialize(writer, value);
        }
    }

    public class CastExtrudeConvertJsonAttribute : Attribute, ICastJson
    {
        public bool CanConvert(MemberInfo member, ParameterInfo paramInfo, IHttpRequest httpRequest, IApplication application, IProvideApiValue apiValueProvider, object objectValue)
        {
            var objectType = member.GetPropertyOrFieldType();
            return CanConvert(objectType, httpRequest, application);
        }

        private bool CanConvert(Type objectType,
            IHttpRequest httpRequest, IApplication application)
        { 
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                var shouldConvertDict = objectType
                    .GetGenericArguments()
                    .Any(item => ShouldConvertDictionaryType(item, httpRequest, application));
                if (shouldConvertDict)
                    return true;
            }
            if (objectType.IsSubclassOf(typeof(Type)))
                return true;
            if (objectType.IsEnum)
                return true;
            return application.GetType()
                .GetAttributesInterface<IConvertJson>()
                .Any(jc => jc.CanConvert(objectType, httpRequest, application));
        }

        protected bool ShouldConvertDictionaryType(Type arg, IHttpRequest httpRequest, IApplication application)
        {
            if (CanConvert(arg, httpRequest, application))
                return true;
            if (arg.ContainsCustomAttribute<IProvideSerialization>(true))
                return true;
            if (arg == typeof(object))
                return true;
            return false;
        }


        public async Task WriteAsync(JsonWriter writer, JsonSerializer serializer,
            MemberInfo member, ParameterInfo paramInfo,
            IProvideApiValue apiValueProvider, object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application)
        {
            var converters = application.GetType()
                .GetAttributesInterface<IConvertJson>()
                .Where(jc => jc.CanConvert(memberValue.GetType(), httpRequest, application));
            if (converters.Any())
            {
                converters.First().Write(writer, memberValue, serializer,
                    httpRequest, application);
                return;
            }

            async Task WriteIdAsync(Guid? idMaybe)
            {
                if (!idMaybe.HasValue)
                {
                    await writer.WriteValueAsync((string)null);
                    return;
                }

                var id = idMaybe.Value;
                await writer.WriteValueAsync(id);
            }

            if (memberValue is IReferenceable)
            {
                var id = (memberValue as IReferenceable).id;
                await WriteIdAsync(id);
                //writer.WriteValue(id);
                return;
            }

            if (memberValue is IReferences)
            {
                writer.WriteStartArray();
                Guid[] ids = await (memberValue as IReferences).ids
                    .Select(
                        async id =>
                        {
                            await WriteIdAsync(id);
                            //writer.WriteValue(id);
                            return id;
                        })
                    .AsyncEnumerable()
                    .ToArrayAsync();
                await writer.WriteEndArrayAsync();
                return;
            }

            if (memberValue is IReferenceableOptional)
            {
                var id = (memberValue as IReferenceableOptional).id;
                await WriteIdAsync(id);
                return;
            }

            if (!memberValue.TryGetType(out Type valueType))
            {
                await writer.WriteNullAsync();
                return;
            }

            if (valueType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                await writer.WriteStartObjectAsync();
                foreach (var kvpObj in memberValue.DictionaryKeyValuePairs())
                {
                    var keyValue = kvpObj.Key;
                    var propertyName = (keyValue is IReferenceable) ?
                        (keyValue as IReferenceable).id.ToString()
                        :
                        keyValue.ToString();
                    writer.WritePropertyName(propertyName);

                    var valueValue = kvpObj.Value;
                    var valueValueType = valueType.GenericTypeArguments.Last();
                    if (this.ShouldConvertDictionaryType(valueValueType, httpRequest, application))
                    {
                        await WriteAsync(writer, serializer, member, paramInfo, apiValueProvider,
                            objectValue, valueValue, httpRequest, application);
                        continue;
                    }
                    await writer.WriteValueAsync(valueValue);
                }
                await writer.WriteEndObjectAsync();
                return;
            }

            if (memberValue is Type)
            {
                var typeValue = (memberValue as Type);
                var serializationAttrs = typeValue
                    .GetAttributesInterface<IProvideSerialization>();
                if (serializationAttrs.Any())
                {
                    var serializationAttr = serializationAttrs
                            .OrderByDescending(x => x.GetPreference(httpRequest))
                            .First();
                    writer.WriteValue(serializationAttr.ContentType);
                    return;
                }
                var stringType = typeValue.GetClrString();
                writer.WriteValue(stringType);
                return;
            }

            if (valueType.IsEnum)
            {
                var stringValue = Enum.GetName(valueType, memberValue);
                writer.WriteValue(stringValue);
                return;
            }

            serializer.Serialize(writer, memberValue);
        }

    }

    public class CastJsonBasicTypesAttribute : Attribute, ICastJson
    {
        public bool CanConvert(MemberInfo member, ParameterInfo paramInfo, IHttpRequest httpRequest, IApplication application, IProvideApiValue apiValueProvider, object objectValue)
        {
            var type = member.GetPropertyOrFieldType();
            return CanConvert(type);

            bool CanConvert(Type type)
            {
                if (typeof(string).IsAssignableFrom(type))
                    return true;
                if (type.IsNumeric())
                    return true;
                if (type.IsEnum)
                    return true;
                if (typeof(bool).IsAssignableFrom(type))
                    return true;
                if (typeof(DateTime).IsAssignableFrom(type))
                    return true;
                if (typeof(TimeSpan).IsAssignableFrom(type))
                    return true;
                return type.IsNullable(
                    baseType => CanConvert(baseType),
                    () => false);
            }
        }

        public async Task WriteAsync(JsonWriter writer, JsonSerializer serializer,
            MemberInfo member, ParameterInfo paramInfo,
            IProvideApiValue apiValueProvider,
            object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application)
        {
            var type = member.GetPropertyOrFieldType();

            await WriteForTypeAsync(type, memberValue);

            async Task WriteForTypeAsync(Type type, object memberValue)
            {
                if (memberValue == null)
                {
                    await writer.WriteNullAsync();
                    return;
                }

                if (typeof(string).IsAssignableFrom(type))
                {
                    await writer.WriteValueAsync((string)memberValue);
                    return;
                }
                if (typeof(bool).IsAssignableFrom(type))
                {
                    await writer.WriteValueAsync((bool)memberValue);
                    return;
                }
                if (type.IsNumeric())
                {
                    await writer.WriteValueAsync(memberValue);
                    return;
                }
                if (type.IsEnum)
                {
                    var enumString = Enum.GetName(type, memberValue);
                    await writer.WriteValueAsync(enumString);
                    return;
                }
                if (typeof(DateTime).IsAssignableFrom(type))
                {
                    await writer.WriteValueAsync((DateTime)memberValue);
                    return;
                }
                if (typeof(TimeSpan).IsAssignableFrom(type))
                {
                    var tsValue = (TimeSpan)memberValue;
                    var writeableValue = tsValue.ToString();
                    await writer.WriteValueAsync(writeableValue);
                    return;
                }
                bool written = await type.IsNullable(
                    async baseType =>
                    {
                        var baseValue = memberValue.GetNullableValue();
                        await WriteForTypeAsync(baseType, baseValue);
                        return true;
                    },
                    () =>
                    {
                        throw new ArgumentException($"{nameof(CastJsonBasicTypesAttribute)}..{nameof(CastJsonBasicTypesAttribute.CanConvert)} said yes but the truth is no.");
                    });
            }
        }

    }
}
