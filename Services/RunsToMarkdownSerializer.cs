using System;
using System.Collections.Generic;
using System.Text;

namespace Seonyx.Web.Services
{
    // Framework-agnostic: no System.Web, no EF6, no SeonyxContext, no
    // HttpContext. Source-linked directly into RoundTripCheckCli
    // (net462) alongside MarkdownInlineParser and
    // InlineXmlRunConverter.

    /// <summary>
    /// Thrown when a run cannot be represented in the Markdown subset
    /// (overlay, smallcaps, or break) -- these have no Markdown
    /// shorthand (amendment section 4.1), so serialization must fail
    /// loudly rather than silently drop the content.
    /// </summary>
    public class NonExpressibleRunException : Exception
    {
        public string Pid { get; private set; }

        public NonExpressibleRunException(string pid, string message)
            : base(message)
        {
            Pid = pid;
        }
    }

    /// <summary>
    /// Inverse of MarkdownInlineParser for the Markdown-expressible
    /// subset only (IsStrong / IsEmph, plain text runs). This class
    /// exists to keep Paragraph.ParagraphText truthful relative to
    /// actual run state after import (see BookML Inline Presentation
    /// P2 plan, decision #6) -- it is not part of the amendment's
    /// original spec, it is a consequence of this codebase still
    /// having a plain-text editor mid-migration.
    ///
    /// Throws NonExpressibleRunException (PID-carrying, same
    /// convention as MarkdownInlineParser's triple-asterisk rejection
    /// and InlineXmlRunConverter's nested-overlay rejection) on any
    /// run with OverlayType != null, IsSmallCaps == true, or
    /// IsBreak == true.
    /// </summary>
    public static class RunsToMarkdownSerializer
    {
        public static string Serialize(IList<InlineRun> runs, string pid)
        {
            var sb = new StringBuilder();

            foreach (var run in runs)
            {
                if (run.OverlayType != null)
                {
                    throw new NonExpressibleRunException(pid, string.Format(
                        "Paragraph {0}: run has OverlayType '{1}', which has no Markdown representation.",
                        pid, run.OverlayType));
                }

                if (run.IsSmallCaps)
                {
                    throw new NonExpressibleRunException(pid, string.Format(
                        "Paragraph {0}: run has IsSmallCaps set, which has no Markdown representation.", pid));
                }

                if (run.IsBreak)
                {
                    throw new NonExpressibleRunException(pid, string.Format(
                        "Paragraph {0}: run has IsBreak set, which has no Markdown representation.", pid));
                }

                string escaped = Escape(run.Text ?? "");

                if (run.IsStrong && run.IsEmph)
                {
                    sb.Append("**_").Append(escaped).Append("_**");
                }
                else if (run.IsStrong)
                {
                    sb.Append("**").Append(escaped).Append("**");
                }
                else if (run.IsEmph)
                {
                    sb.Append("*").Append(escaped).Append("*");
                }
                else
                {
                    sb.Append(escaped);
                }
            }

            return sb.ToString();
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '*' || c == '_' || c == '\\')
                    sb.Append('\\');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
