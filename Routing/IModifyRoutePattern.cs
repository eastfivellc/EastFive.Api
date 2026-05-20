using System.Reflection;
using System.Text;

namespace EastFive.Api
{
    /// <summary>
    /// Attribute-interface implemented by parameter attributes that need to
    /// contribute to or otherwise modify the URL regex pattern under which
    /// their owning method is matched (template-based routing).
    ///
    /// The framework iterates each parameter attribute that implements this
    /// interface during <see cref="HttpVerbAttribute.GetRouteTemplate"/> and
    /// gives it the pattern-so-far. Every modifier runs, in parameter /
    /// attribute declaration order; each sees the output of the previous
    /// one. An attribute that wants to opt out simply returns the input
    /// string unchanged.
    ///
    /// Decoupling: this replaces the prior <c>TryGetFileNameCaptureKey</c>
    /// helper that was hard-wired to <c>[QueryId]</c>,
    /// <c>[QueryParameter(CheckFileName = true)]</c>, and
    /// <see cref="IProvideBindingRequirements"/>. Each attribute now owns
    /// the regex fragment it wants to inject, and composite-key /
    /// multi-segment routes can layer multiple modifiers cleanly.
    /// </summary>
    public interface IModifyRoutePattern
    {
        /// <summary>
        /// Mutate (or pass through) the route regex pattern in light of this
        /// parameter. Return <paramref name="currentPattern"/> unchanged to
        /// signal "no contribution".
        /// </summary>
        string ModifyRoutePattern(MethodInfo method, ParameterInfo parameter, string currentPattern);
    }

    /// <summary>
    /// Helpers for <see cref="IModifyRoutePattern"/> implementers that just
    /// want to append a single trailing named-capture group (the common
    /// /api/Resource/{id} case).
    /// </summary>
    public static class RoutePattern
    {
        /// <summary>
        /// Append <c>(?:/(?&lt;key&gt;.*))?</c> to <paramref name="current"/>.
        /// Returns <paramref name="current"/> unchanged when
        /// <paramref name="rawCaptureName"/> is empty / sanitises to empty.
        /// </summary>
        public static string AppendTrailingCapture(string current, string rawCaptureName)
        {
            var captureName = SanitizeCaptureName(rawCaptureName);
            if (string.IsNullOrEmpty(captureName))
                return current;
            return current + "(?:/(?<" + captureName + ">.*))?";
        }

        /// <summary>
        /// Strip characters not legal in a .NET regex named-capture group
        /// (keeps letters, digits, underscore; prefixes a leading underscore
        /// when the result starts with a digit).
        /// </summary>
        public static string SanitizeCaptureName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;
            var buf = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    buf.Append(c);
            }
            if (buf.Length == 0)
                return null;
            if (char.IsDigit(buf[0]))
                buf.Insert(0, '_');
            return buf.ToString();
        }
    }
}
