using System;

namespace Seonyx.Web.Services
{
    /// <summary>
    /// Structural breaks (BookML &lt;break type="scene|section|page"/&gt;).
    /// A break is stored as a working-copy Paragraph row with no text and no
    /// runs, a pid using the B type code (e.g. CH01-B0001), and its kind in
    /// its ParagraphVersion.ParaType ("break-scene", "break-section",
    /// "break-page"). Every reader decides "is this a break, and which kind"
    /// here.
    /// </summary>
    public static class BreakKinds
    {
        public const string Scene   = "break-scene";
        public const string Section = "break-section";
        public const string Page    = "break-page";

        private const string Prefix = "break-";

        /// <summary>
        /// The scene-break symbol used in every export ("the smiling toad"):
        /// bullet, em dash, bullet -- characters present in almost all fonts.
        /// Written as escapes so this source file stays plain ASCII.
        /// </summary>
        public const string SceneBreakSymbol = "\u2022 \u2014 \u2022";

        public static bool IsBreak(string paraType)
        {
            return paraType != null && paraType.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>BookML break type ("scene"/"section"/"page") for a ParaType, or null if not a break.</summary>
        public static string ToBookmlType(string paraType)
        {
            return IsBreak(paraType) ? paraType.Substring(Prefix.Length) : null;
        }

        /// <summary>ParaType for a BookML break type attribute; defaults to a scene break.</summary>
        public static string FromBookmlType(string bookmlType)
        {
            return Prefix + (string.IsNullOrEmpty(bookmlType) ? "scene" : bookmlType);
        }
    }
}
