using System;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Marker helpers used inside a scripted flow expression. None of these execute at
    /// runtime — each one is recognized by <see cref="FlowScriptReader"/> as a specific node
    /// in the captured expression tree:
    /// <list type="bullet">
    /// <item><see cref="NewId{T}"/> renders a client-generated id as <c>{{$guid}}</c>.</item>
    /// <item><see cref="Input{T}"/> renders an externally supplied environment variable as <c>{{name}}</c>.</item>
    /// </list>
    /// </summary>
    public static class Flow
    {
        /// <summary>
        /// A client-generated identifier. In the emitted request body this renders as the
        /// Postman dynamic variable <c>{{$guid}}</c>. Assign it to an <see cref="IRef{T}"/>
        /// member of a resource body to mint that resource's id on the client.
        /// </summary>
        public static IRef<T> NewId<T>()
            where T : IReferenceable
            => default;

        /// <summary>
        /// An externally supplied value referenced by Postman environment variable name. In
        /// the emitted request this renders as <c>{{name}}</c>. Use for flow inputs that are
        /// provided by the environment rather than produced by an earlier step.
        /// </summary>
        public static T Input<T>(string name)
            => default;
    }
}
