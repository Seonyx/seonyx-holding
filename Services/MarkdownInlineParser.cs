using System;
using System.Collections.Generic;
using System.Text;

namespace Seonyx.Web.Services
{
    // Framework-agnostic: no System.Web, no EF6, no SeonyxContext, no
    // HttpContext. This file is source-linked directly into
    // RoundTripCheckCli (net462), so it must build standalone against
    // the plain .NET Framework 4.6.2 BCL only.

    /// <summary>
    /// One style-tagged text fragment produced by parsing the Markdown
    /// inline subset. Not a ParagraphRun -- callers convert/coalesce
    /// these into runs themselves.
    /// </summary>
    public class MarkdownFragment
    {
        public string Text { get; set; }
        public bool IsStrong { get; set; }
        public bool IsEmph { get; set; }
    }

    /// <summary>
    /// Thrown when the input text is not valid within the amendment
    /// section 4.1 Markdown subset (e.g. triple-asterisk emphasis).
    /// </summary>
    public class MarkdownParseException : Exception
    {
        public string Pid { get; private set; }

        public MarkdownParseException(string pid, string message)
            : base(message)
        {
            Pid = pid;
        }
    }

    /// <summary>
    /// Parses the deliberately small Markdown subset defined in the
    /// BookML Inline Presentation Elements amendment, section 4.1:
    ///   **text**        -> strong
    ///   *text* / _text_ -> emph
    ///   \*  \_  \\       -> literal escapes
    /// Triple-asterisk is rejected. Underscores inside a word are
    /// literal, not emph markers. No syntax for smallcaps or line
    /// breaks -- both are editor-only actions.
    /// </summary>
    public static class MarkdownInlineParser
    {
        public static IList<MarkdownFragment> Parse(string text, string pid)
        {
            var fragments = new List<MarkdownFragment>();
            if (string.IsNullOrEmpty(text))
                return fragments;

            int i = 0;
            int len = text.Length;
            var current = new StringBuilder();
            bool strong = false;
            bool emph = false;
            char? emphMarker = null; // '*' or '_' -- must match on close

            Action flush = () =>
            {
                if (current.Length > 0)
                {
                    fragments.Add(new MarkdownFragment
                    {
                        Text = current.ToString(),
                        IsStrong = strong,
                        IsEmph = emph
                    });
                    current.Clear();
                }
            };

            while (i < len)
            {
                char c = text[i];

                // Backslash escapes: \* \_ \\ only.
                if (c == '\\' && i + 1 < len && IsEscapable(text[i + 1]))
                {
                    current.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '*')
                {
                    // Triple-asterisk is explicitly rejected.
                    if (i + 2 < len && text[i + 1] == '*' && text[i + 2] == '*')
                    {
                        throw new MarkdownParseException(pid,
                            string.Format("Paragraph {0}: triple-asterisk emphasis (***) is not supported -- use nested **_x_** or *__x__* instead.", pid));
                    }

                    if (i + 1 < len && text[i + 1] == '*')
                    {
                        // ** toggles strong
                        flush();
                        strong = !strong;
                        i += 2;
                        continue;
                    }

                    // single * toggles emph, must not be mid-word-adjacent
                    // in a way that breaks pairing -- treat every lone '*'
                    // as a toggle (simple, matches the amendment's small
                    // subset; mismatched markers just fold the rest of
                    // the text into the open style, same as CommonMark
                    // does for unterminated emphasis in casual practice).
                    flush();
                    if (emph && emphMarker == '*')
                    {
                        emph = false;
                        emphMarker = null;
                    }
                    else if (!emph)
                    {
                        emph = true;
                        emphMarker = '*';
                    }
                    else
                    {
                        // emph open with a different marker ('_') -- '*'
                        // here is literal text, not a toggle.
                        current.Append(c);
                    }
                    i++;
                    continue;
                }

                if (c == '_')
                {
                    bool leftAlnum = i > 0 && IsWordChar(text[i - 1]);
                    bool rightAlnum = i + 1 < len && IsWordChar(text[i + 1]);

                    // Mid-word underscore (foo_bar_baz) is literal.
                    if (leftAlnum && rightAlnum)
                    {
                        current.Append(c);
                        i++;
                        continue;
                    }

                    flush();
                    if (emph && emphMarker == '_')
                    {
                        emph = false;
                        emphMarker = null;
                    }
                    else if (!emph)
                    {
                        emph = true;
                        emphMarker = '_';
                    }
                    else
                    {
                        // emph open with '*' -- '_' here is literal.
                        current.Append(c);
                    }
                    i++;
                    continue;
                }

                current.Append(c);
                i++;
            }

            flush();
            return fragments;
        }

        private static bool IsEscapable(char c)
        {
            return c == '*' || c == '_' || c == '\\';
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c);
        }
    }
}
