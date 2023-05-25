using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;

using EastFive.Api.Resources;
using EastFive;
using EastFive.Extensions;
using EastFive.Reflection;
using EastFive.Text;
using EastFive.Collections.Generic;
using EastFive.Linq;
using EastFive.Serialization;

namespace EastFive.Api.Bindings
{
    public class StandardStringBindingsAttribute : Attribute,
        IBindParameter<string>, IBindApiParameter<string>,
        IBindParameter<byte[]>, IBindApiParameter<byte[]>
    {
        public delegate object BindingDelegate(
                StandardStringBindingsAttribute httpApp,
                string content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        #region Strings

        public TResult Bind<TResult>(ParameterInfo parameter, string content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(Type type, string content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind: (why) =>
                {
                    if (application is IApiApplication)
                    {
                        var apiApplication = application as IApiApplication;
                        if (type == typeof(Type))
                        {
                            return apiApplication.GetResourceType(content,
                                type => onParsed(type),
                                () => onDidNotBind($"Could not find type:{content}"));
                        }
                    }
                    return onDidNotBind(why);
                },
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
                if (content.IsNullOrWhiteSpace())
                    return onParsed(new Guid[] { });
                if(content.StartsWith('['))
                {
                    content = content
                        .TrimStart('[')
                        .TrimEnd(']');
                }
                var tokens = content.Split(','.AsArray());
                var guids = tokens
                    .Select(
                        token => BindDirect(typeof(Guid), token,
                                    guid => guid,
                                    (why) => default(Guid?),
                                    (why) => default(Guid?)))
                    .Cast<Guid?>()
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToArray();
                return onParsed(guids);
            }
            if (type == typeof(DateTime))
            {
                return ParseDate(content,
                    (currentDateString) => onDidNotBind(
                        $"Failed to convert {content} to `{typeof(DateTime).FullName}`."));

                TResult ParseDate(string dateString, Func<string, TResult> onParseFailed)
                {
                    if(dateString.IsNullOrWhiteSpace())
                        return onParseFailed(dateString);

                    if (DateTime.TryParse(dateString, out DateTime dateValue))
                        return onParsed(dateValue);

                    // Common format not supported by TryParse
                    if (DateTime.TryParseExact(dateString, "ddd MMM d yyyy HH:mm:ss 'GMT'K",
                            null, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out dateValue))
                        return onParsed(dateValue);

                    var startOfDescText = dateString.IndexOf('(');
                    if (startOfDescText > 0)
                    {
                        var cleanerText = content.Substring(0, startOfDescText);
                        return ParseDate(cleanerText,
                            failedText =>
                            {
                                var decodedContent = System.Net.WebUtility.UrlDecode(failedText);
                                if (decodedContent != failedText)
                                    return ParseDate(decodedContent,
                                        (failedDecodedText) => onParseFailed(failedDecodedText));
                                return onParseFailed(failedText);
                            });
                    }

                    var decodedContent = System.Net.WebUtility.UrlDecode(dateString);
                    if (decodedContent != dateString)
                        return ParseDate(decodedContent,
                            (failedDecodedText) => onParseFailed(failedDecodedText));
                    return onParseFailed(dateString);
                }

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
                if (content.IsDefaultNullOrEmpty())
                    return onDidNotBind("Value not provided.");

                if (content.TryParseBool(out var boolValue))
                    return onParsed(boolValue);

                return onDidNotBind($"Failed to convert {content} to `{typeof(bool).FullName}`.");
            }
            if (type == typeof(Uri))
            {
                if (content.IsDefaultNullOrEmpty())
                    return onBindingFailure("URL value was empty");
                if (Uri.TryCreate(content.Trim('"'.AsArray()), UriKind.RelativeOrAbsolute, out Uri uriValue))
                    return onParsed(uriValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(Uri).FullName}`.");
            }
            if (type == typeof(Type))
            {
                return content.GetClrType(
                    typeInstance => onParsed(typeInstance),
                    () => onDidNotBind(
                        $"`{content}` is not a recognizable resource type or CLR type."));
                //() => HttpApplication.GetResourceType(content,
                //        (typeInstance) => onParsed(typeInstance),
                //        () => content.GetClrType(
                //            typeInstance => onParsed(typeInstance),
                //            () => onDidNotBind(
                //                $"`{content}` is not a recognizable resource type or CLR type."))));
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
                if (content.TryParseBase64String(out byte[] byteArrayValue))
                    return onParsed(byteArrayValue);
                return onDidNotBind($"Failed to convert {content} to `{typeof(byte[]).FullName}` as base64 string.");
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
                if(Enum.TryParse(type, content, out object value))
                    return onParsed(value);

                var validValues = Enum.GetNames(type).Join(", ");
                return onDidNotBind($"Value `{content}` is not a valid value for `{type.FullName}.` Valid values are [{validValues}].");
            }

            if(type.IsArray)
            {
                return content.MatchRegexInvoke(
                    @"(\[(?<index>[0-9]+)\]=)?(?<value>([^\;]|(?<=\\)\;)+)",
                    (index, value) => index.PairWithValue(value),
                    onMatched: tpls =>
                    {
                        // either abc;def
                        // or [0]=abc;[1]=def
                        var matchesDictionary = tpls.Any(kvp => string.IsNullOrEmpty(kvp.Key))
                            ? tpls
                            .Select(
                                (kvp, index) => kvp.Value.PairWithKey(index))
                            .ToDictionary()
                            : tpls
                            .TryWhere(
                                (KeyValuePair<string, string> kvp, out int indexedValue) =>
                                    int.TryParse(kvp.Key, out indexedValue))
                            .Select(
                                match => match.item.Value.PairWithKey(match.@out))
                            .ToDictionary();


                        // matchesDictionary.Keys will throw if empty
                        var ordered = matchesDictionary.IsDefaultNullOrEmpty()?
                            new (bool, string)[] { }
                            :
                            Enumerable
                                .Range(0, matchesDictionary.Keys.Max() + 1)
                                .Select(
                                    (index) =>
                                    {
                                        if (!matchesDictionary.TryGetValue(index, out string value))
                                            return (false, $"Missing index {index}");
                                        return (true, value);
                                    })
                                .ToArray();

                        var arrayType = type.GetElementType();
                        return ordered
                            .Where(tpl => !tpl.Item1)
                            .First(
                                (tpl, next) => onBindingFailure(tpl.Item2),
                                () =>
                                {
                                    var parsed = ordered
                                        .SelectWhere()
                                        .Select(value => BindDirect(arrayType, value,
                                            v => (0, v),
                                            why => (1, why),
                                            why => (2, why)))
                                        .ToArray();
                                    return parsed
                                        .Where(tpl => tpl.Item1 != 0)
                                        .First(
                                            (tpl, next) => onBindingFailure((string)tpl.Item2),
                                            () =>
                                            {
                                                var value = parsed
                                                    .Select(tpl => tpl.Item2)
                                                    .ToArray()
                                                    .CastArray(arrayType);
                                                return onParsed(value);
                                            });
                                });
                         
                    });
                // return onDidNotBind($"Array not formatted correctly. It must be [0]=asdf;[1]=qwer;[2]=zxcv");
            }

            return onDidNotBind($"No binding for type `{type.FullName}` provided by {typeof(StandardStringBindingsAttribute).FullName}.");
        }

        #endregion

        #region Bytes

        public TResult Bind<TResult>(ParameterInfo parameter, byte [] content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(Type type, byte[] content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind,
                onBindingFailure);
        }

        public static TResult BindDirect<TResult>(Type type, byte[] content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (type == typeof(Guid))
            {
                if (content.Length == 0x10)
                {
                    var guid = new Guid(content);
                    return onParsed(guid);
                }

                {
                    var byteSpan = (ReadOnlySpan<byte>)content;
                    var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);
                    if (Guid.TryParse(charSpan, out Guid guid))
                        return onParsed(guid);
                }
            }

            var stringValue = System.Text.Encoding.UTF8.GetString(content);
            return BindDirect(type, stringValue,
                onParsed,
                onDidNotBind,
                onBindingFailure);
        }

        #endregion

    }
}
