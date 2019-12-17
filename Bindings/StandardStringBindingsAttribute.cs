using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Bindings
{
    public class StandardStringBindingsAttribute : Attribute, IBindApiParameter<string>
    {
        public delegate object BindingDelegate(
                StandardStringBindingsAttribute httpApp,
                string content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        public TResult Bind<TResult>(Type type, string content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind,
                onBindingFailure);
        }
 
        public static TResult BindDirect<TResult>(Type type, string content, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (type == typeof(string))
            {
                var stringValue = content;
                return onParsed((object)stringValue);
            }
            if (type == typeof(Guid))
            {
                if (Guid.TryParse(content, out Guid stringGuidValue))
                    return onParsed(stringGuidValue);
                return onBindingFailure($"Failed to convert `{content}` to type `{typeof(Guid).FullName}`.");
            }
            if (type == typeof(Guid[]))
            {
                var tokens = content.Split(','.AsArray());
                var guids = tokens
                    .Select(
                        token => BindDirect(typeof(Guid), token,
                                    guid => guid,
                                    (why) => default(Guid),
                                    (why) => default(Guid)))
                    .Cast<Guid>()
                    .ToArray();
                return onParsed(guids);
            }
            if (type == typeof(DateTime))
            {
                if (DateTime.TryParse(content, out DateTime dateValue))
                    return onParsed(dateValue);
                return onDidNotBind($"Failed to convert {content} to `{typeof(DateTime).FullName}`.");
            }
            if (type == typeof(DateTimeOffset))
            {
                if (DateTimeOffset.TryParse(content, out DateTimeOffset dateValue))
                    return onParsed(dateValue);
                return onDidNotBind($"Failed to convert {content} to `{typeof(DateTimeOffset).FullName}`.");
            }
            if (type == typeof(int))
            {
                if (int.TryParse(content, out int intValue))
                    return onParsed(intValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(int).FullName}`.");
            }
            if (type == typeof(double))
            {
                if (double.TryParse(content, out double doubleValue))
                    return onParsed(doubleValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(double).FullName}`.");
            }
            if (type == typeof(decimal))
            {
                if (decimal.TryParse(content, out decimal decimalValue))
                    return onParsed(decimalValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(decimal).FullName}`.");
            }
            if (type == typeof(bool))
            {
                if ("t" == content.ToLower())
                    return onParsed(true);

                if ("on" == content.ToLower()) // used in check boxes
                    return onParsed(true);

                if ("f" == content)
                    return onParsed(false);

                if ("off" == content.ToLower()) // used in some check boxes
                    return onParsed(false);

                // TryParse may convert "on" to false TODO: Test theory
                if (bool.TryParse(content, out bool boolValue))
                    return onParsed(boolValue);

                return onDidNotBind($"Failed to convert {content} to `{typeof(bool).FullName}`.");
            }
            if (type == typeof(Uri))
            {
                if (Uri.TryCreate(content, UriKind.RelativeOrAbsolute, out Uri uriValue))
                    return onParsed(uriValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(Uri).FullName}`.");
            }
            if (type == typeof(Type))
            {
                return HttpApplication.GetResourceType(content,
                    (typeInstance) => onParsed(typeInstance),
                    () => content.GetClrType(
                        typeInstance => onParsed(typeInstance),
                        () => onDidNotBind(
                            $"`{content}` is not a recognizable resource type or CLR type.")));
            }
            if (type == typeof(Stream))
            {
                return BindDirect(typeof(byte[]), content,
                    byteArrayValueObj =>
                    {
                        var byteArrayValue = (byte[])byteArrayValueObj;
                        return onParsed(new MemoryStream(byteArrayValue));
                    },
                    onDidNotBind,
                    onBindingFailure);
            }
            if (type == typeof(byte[]))
            {
                try
                {
                    var byteArrayValue = Convert.FromBase64String(content);
                    return onParsed(byteArrayValue);
                }
                catch (Exception ex)
                {
                    return onDidNotBind($"Failed to convert {content} to `{typeof(byte[]).FullName}` as base64 string:{ex.Message}.");
                }
            }
            if (type == typeof(WebId))
            {
                if (!Guid.TryParse(content, out Guid guidValue))
                    return onBindingFailure($"Could not convert `{content}` to GUID");
                var webIdObj = (object)new WebId() { UUID = guidValue };
                return onParsed(webIdObj);
            }
            if (type == typeof(Controllers.DateTimeEmpty))
            {
                if (String.Compare(content.ToLower(), "false") == 0)
                    return onParsed(new Controllers.DateTimeEmpty());
                return onBindingFailure($"Failed to convert {content} to `{typeof(Controllers.DateTimeEmpty).FullName}`.");
            }
            if (type == typeof(Controllers.DateTimeQuery))
            {
                if (DateTime.TryParse(content, out DateTime startEnd))
                    return onParsed(new Controllers.DateTimeQuery(startEnd, startEnd));
                return onBindingFailure($"Failed to convert {content} to `{typeof(Controllers.DateTimeQuery).FullName}`.");
            }
            if (type == typeof(object))
            {
                var objValue = content;
                return onParsed(objValue);
            }

            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                return BindDirect(typeof(Guid), content,
                    (id) =>
                    {
                        var resourceType = type.GenericTypeArguments.First();
                        var instantiatableType = typeof(EastFive.Ref<>).MakeGenericType(resourceType);
                        var instance = Activator.CreateInstance(instantiatableType, new object[] { id });
                        return onParsed(instance);
                    },
                    onDidNotBind,
                    (why) => onBindingFailure(why));

            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                var referredType = type.GenericTypeArguments.First();

                TResult emptyOptional()
                {
                    var refInst = RefOptionalHelper.CreateEmpty(referredType);
                    return onParsed(refInst);
                };

                if (content.IsNullOrWhiteSpace())
                    return emptyOptional();
                if (content.ToLower() == "empty")
                    return emptyOptional();
                if (content.ToLower() == "null")
                    return emptyOptional();

                var refType = typeof(IRef<>).MakeGenericType(referredType);
                return BindDirect(refType, content,
                    (v) =>
                    {
                        var refOptionalType = typeof(RefOptional<>).MakeGenericType(referredType);
                        var refInst = Activator.CreateInstance(refOptionalType, new object[] { v });
                        return onParsed(refInst);
                    },
                    (why) => emptyOptional(),
                    (why) => emptyOptional());
            }
            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                return BindDirect(typeof(Guid[]), content,
                    (ids) =>
                    {
                        var resourceType = type.GenericTypeArguments.First();
                        var instantiatableType = typeof(Refs<>).MakeGenericType(resourceType);
                        var instance = Activator.CreateInstance(instantiatableType, new object[] { ids });
                        return onParsed(instance);
                    },
                    onDidNotBind,
                    (why) => onBindingFailure(why));
            }
            if (type.IsSubClassOfGeneric(typeof(Nullable<>)))
            {
                var underlyingType = type.GetNullableUnderlyingType();
                return BindDirect(underlyingType, content,
                    (nonNullable) =>
                    {
                        var nullable = nonNullable.AsNullable();
                        return onParsed(nullable);
                    },
                    (why) => onParsed(type.GetDefault()),
                    (why) => onParsed(type.GetDefault()));
            }

            if (type.IsEnum)
            {
                object value;
                try
                {
                    value = Enum.Parse(type, content);
                }
                catch (Exception)
                {
                    var validValues = Enum.GetNames(type).Join(", ");
                    return onDidNotBind($"Value `{content}` is not a valid value for `{type.FullName}.` Valid values are [{validValues}].");
                }
                return onParsed(value);
            }

            return onDidNotBind($"No binding for type `{type.FullName}` provided by {typeof(StandardStringBindingsAttribute).FullName}.");
        }

    }
}
