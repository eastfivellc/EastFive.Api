using System.Reflection;

namespace EastFive.Api
{
    /// <summary>
    /// Marker + provider for parameter attributes whose values come from the
    /// inbound request (URL, query string, body, form, multipart, etc.).
    /// Implementing this interface is what makes an attribute "binding-class"
    /// in v3 routing: the selector iterates each binding-class parameter,
    /// calls <see cref="GetRequirement"/>, and asks the chosen
    /// <see cref="IRequestEnvelope"/> whether it can fulfill the requirement.
    /// Parameters whose attributes do not implement this interface are
    /// instigator parameters resolved via <c>httpApp.Instigate(...)</c>.
    ///
    /// Implementing attributes: <c>[QueryId]</c>, <c>[Property]</c>,
    /// <c>[PropertyOptional]</c>, <c>[QueryParameter]</c>,
    /// <c>[OptionalQueryParameter]</c>, <c>[Resource]</c>, <c>[UpdateId]</c>.
    ///
    /// v2 routing is unaffected — it continues to use the attributes' existing
    /// <c>TryCast</c> logic.
    /// </summary>
    public interface IProvideBindingRequirement
    {
        /// <summary>
        /// Build a typed <see cref="BindingRequirement{T}"/> describing what
        /// this parameter needs from the request envelope. The concrete
        /// <c>T</c> is chosen by the attribute (e.g. <c>string</c> for a
        /// query parameter, <c>JToken</c> for a JSON-bound property).
        /// </summary>
        BindingRequirement GetRequirement(ParameterInfo parameter);
    }
}
