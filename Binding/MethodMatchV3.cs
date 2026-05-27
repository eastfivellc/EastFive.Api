using System;
using System.Reflection;

using EastFive.Api.Routing;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// One <see cref="RouteCandidate"/> resolved against the V3 envelope: every
    /// parameter that carries an <see cref="IBindFromRequest"/> attribute matched,
    /// and its <see cref="BindCall"/> contribution lives inside
    /// <see cref="Source"/>, keyed by parameter name.
    /// <para>
    /// Produced by <see cref="MethodDispatcherV3.BuildMatches"/>; the V3 selector
    /// is total — non-matching candidates are dropped at build time, so there is
    /// no <c>IsValid</c> flag here (compare <see cref="MethodMatch"/>).
    /// </para>
    /// </summary>
    public readonly struct MethodMatchV3
    {
        public MethodMatchV3(Type controllerType, IInvokeResource invokeResource,
            MethodInfo method, CompositeBindingSource source)
        {
            this.ControllerType = controllerType;
            this.InvokeResource = invokeResource;
            this.Method = method;
            this.Source = source;
        }

        public Type ControllerType { get; }
        public IInvokeResource InvokeResource { get; }
        public MethodInfo Method { get; }
        public CompositeBindingSource Source { get; }
    }
}
