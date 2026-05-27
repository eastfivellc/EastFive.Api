using System.Reflection;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 method-selection contract for parameter attributes. The dispatcher walks
    /// every candidate method's parameters; any parameter carrying an attribute
    /// that implements this interface participates in selection. The attribute
    /// either produces a <see cref="BindCall"/> closure rooted at the parameter's
    /// value (selection match) or returns <c>false</c> (this method doesn't apply).
    /// <para>
    /// Selection is intentionally <b>synchronous</b>: the envelope's data is
    /// already materialized (JToken / form collection / dict in memory), and
    /// selection runs across every candidate so cost must stay low.
    /// </para>
    /// <para>
    /// <b>Closure shape.</b> Attributes return a <see cref="BindCall"/> rather
    /// than an <see cref="EastFive.Serialization.Binding.IBindingSource"/> — the
    /// scalar cases (Query, Route, Header) capture the single value in a closure
    /// instead of allocating a <c>LookupBindingSource</c> + path-prefix wrapper
    /// for each parameter on each request. Body attributes wrap an existing
    /// <see cref="EastFive.Serialization.Binding.IBindingSource"/> via
    /// <see cref="BindCalls.FromSource"/>. Optional-absent contributions return
    /// <see cref="BindCalls.NotPresent"/> rather than <c>false</c>.
    /// </para>
    /// <para>
    /// Surface validation (is it a Guid? a date?) is <b>not</b> the attribute's
    /// job — that belongs to the bind phase via <c>TypeBindings</c>.
    /// </para>
    /// </summary>
    public interface IBindFromRequest
    {
        /// <summary>
        /// Return <c>true</c> with a <paramref name="call"/> closure rooted at the
        /// parameter's value, or <c>false</c> if this envelope cannot supply it.
        /// </summary>
        bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call);
    }
}
