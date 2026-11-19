using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Seonyx.Web.Services
{
    /// <summary>
    /// Paragraph runs to EPUB XHTML, per BookML Amendment 2 section 2:
    ///   emph -> em (with class emph-X when it has a type), strong -> strong,
    ///   smallcaps -> span.smallcaps, linebreak -> br,
    ///   overlay -> class "overlay overlay-X": a span when the overlay sits
    ///   inside a sentence, an aside wrapping the paragraph when the whole
    ///   paragraph is one overlay (an aside is not allowed inside a p).
    /// No database access, so it can be tested on its own.
    /// </summary>
    public static class RunsToXhtml
    {
        /// <summary>
        /// Amendment 2 section 2.4: lower-case; anything not a-z, 0-9 or "-"
        /// becomes "-"; collapse runs of "-"; trim "-". Returns null if
        /// nothing is left (then only the baseline class is emitted).
        /// </summary>
        public static string NormaliseClassSuffix(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;
            var sb = new StringBuilder();
            foreach (var ch in type.ToLowerInvariant())
            {
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-';
                char c = ok ? ch : '-';
                if (c == '-' && sb.Length > 0 && sb[sb.Length - 1] == '-') continue;
                sb.Append(c);
            }
            var result = sb.ToString().Trim('-');
            return result.Length == 0 ? null : result;
        }

        public static string OverlayClass(string overlayType)
        {
            var suffix = NormaliseClassSuffix(overlayType);
            return suffix == null ? "overlay" : "overlay overlay-" + suffix;
        }

        /// <summary>
        /// A complete paragraph: an aside wrapping a p when every text run is
        /// in one overlay, otherwise a p (with cssClass when given).
        /// </summary>
        public static string RenderParagraph(IList<InlineRun> runs, string cssClass)
        {
            var classAttr = string.IsNullOrEmpty(cssClass) ? "" : " class=\"" + Escape(cssClass) + "\"";

            string wholeType;
            if (IsWholeParagraphOverlay(runs, out wholeType))
            {
                var plain = runs.Select(r => new InlineRun
                {
                    Text = r.Text, IsEmph = r.IsEmph, EmphType = r.EmphType, IsStrong = r.IsStrong,
                    IsSmallCaps = r.IsSmallCaps, OverlayType = null, IsBreak = r.IsBreak
                }).ToList();
                return "<aside class=\"" + Escape(OverlayClass(wholeType)) + "\"><p" + classAttr + ">"
                    + RenderInline(plain) + "</p></aside>";
            }

            return "<p" + classAttr + ">" + RenderInline(runs) + "</p>";
        }

        /// <summary>The inline content of a paragraph (what goes inside its p).</summary>
        public static string RenderInline(IList<InlineRun> runs)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < runs.Count)
            {
                var overlay = runs[i].IsBreak ? null : runs[i].OverlayType;
                if (overlay == null)
                {
                    sb.Append(RenderRun(runs[i]));
                    i++;
                    continue;
                }

                // Consecutive runs sharing this overlay type form one span.
                sb.Append("<span class=\"").Append(Escape(OverlayClass(overlay))).Append("\">");
                while (i < runs.Count && !runs[i].IsBreak && runs[i].OverlayType == overlay)
                {
                    sb.Append(RenderRun(runs[i]));
                    i++;
                }
                sb.Append("</span>");
            }
            return sb.ToString();
        }

        private static string RenderRun(InlineRun r)
        {
            if (r.IsBreak) return "<br/>";

            var text = Escape(r.Text ?? "");
            if (text.Length == 0) return "";

            if (r.IsSmallCaps) text = "<span class=\"smallcaps\">" + text + "</span>";
            if (r.IsEmph)
            {
                var suffix = NormaliseClassSuffix(r.EmphType);
                text = (suffix == null ? "<em>" : "<em class=\"emph-" + suffix + "\">") + text + "</em>";
            }
            if (r.IsStrong) text = "<strong>" + text + "</strong>";
            return text;
        }

        private static bool IsWholeParagraphOverlay(IList<InlineRun> runs, out string overlayType)
        {
            overlayType = null;
            var textRuns = runs.Where(r => !r.IsBreak && !string.IsNullOrEmpty(r.Text)).ToList();
            if (textRuns.Count == 0) return false;
            var first = textRuns[0].OverlayType;
            if (first == null || textRuns.Any(r => r.OverlayType != first)) return false;
            overlayType = first;
            return true;
        }

        /// <summary>XML escaping for text and attribute values; non-ASCII stays as UTF-8.</summary>
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
