using System;
using System.Linq;
using System.Reflection;

using EastFive.Api.Binding.Scopes;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 attribute that locates a method parameter inside the request body.
    /// Path defaults to the parameter's name; <see cref="Name"/> overrides
    /// (or the empty string to bind the entire body to the parameter).
    /// <para>
    /// Selection succeeds when the body exists in any supported raw shape.
    /// Whether the path resolves to a present value is decided at bind time —
    /// the underlying <see cref="EastFive.Serialization.Binding.IBindingSource"/>
    /// reports <see cref="EastFive.Serialization.Binding.NotPresent"/> which the
    /// bind phase translates into the parameter's C# default value when present,
    /// otherwise into a bind failure.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class BodyAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        /// <summary>Path inside the body. Defaults to the parameter name.</summary>
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (!EnvelopeBodyAccessor.TryGetBodyRoot(envelope, out var root, out var rawBody))
            {
                if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
                call = null;
                return false;
            }
            call = BindCalls.FromSource(root, Name ?? parameter.Name);
            return true;
        }
    }

    /// <summary>
    /// V3 attribute that binds the parameter to the <b>entire</b> body — the
    /// canonical "deserialize the request payload into this POCO" case.
    /// Equivalent to <c>[Body(Name = "")]</c> but expresses intent more clearly
    /// at the call site. Selection misses if no body is present.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class ResourceAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (EnvelopeBodyAccessor.TryGetBodyRoot(envelope, out var root, out var rawBody))
            {
                call = BindCalls.FromSource(root, string.Empty);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that locates the parameter in the URL query string.
    /// Case-insensitive key matching (ASP.NET conventional). Multi-valued keys
    /// dispatch via <c>onArray</c>; single values via <c>onString</c>.
    /// Required-but-absent = selection miss; optional-absent =
    /// <see cref="BindCalls.NotPresent"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class QueryAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            if (envelope.Query.TryGetValue(key, out var values) && values is { Length: > 0 })
            {
                call = BindCalls.MultiValue(key, values);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that locates an OPTIONAL parameter in the URL query string.
    /// Behaves like <see cref="QueryAttribute"/> when the key is present, but is
    /// never a selection miss: an absent value contributes
    /// <see cref="BindCalls.Null"/>, so a <c>Nullable&lt;T&gt;</c> or reference-type
    /// parameter binds to <c>null</c> even without a C# default value. Apply this
    /// to make <c>start</c>/<c>days</c>-style query parameters optional.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class QueryOptionalAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            if (envelope.Query.TryGetValue(key, out var values) && values is { Length: > 0 })
            {
                call = BindCalls.MultiValue(key, values);
                return true;
            }
            call = BindCalls.Null;
            return true;
        }
    }

    /// <summary>
    /// V3 attribute that locates the parameter in route-template captures
    /// (regex group names, conventional <c>{id}</c> tokens). Route values are
    /// always single-valued strings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class RouteAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            if (envelope.Route.TryGetValue(key, out var value))
            {
                call = BindCalls.Scalar(value);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that locates the parameter in HTTP request headers. Header
    /// names are case-insensitive per HTTP spec. Multi-valued headers dispatch
    /// via <c>onArray</c>; single values via <c>onString</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class HeaderAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            var values = envelope.Request.GetHeaders(key);
            var arr = values is null ? Array.Empty<string>() : values.ToArray();
            if (arr.Length > 0)
            {
                call = BindCalls.MultiValue(key, arr);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }
}
