using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    public class ParagraphEntry
    {
        public string Pid  { get; set; }
        public int    Seq  { get; set; }
        public string Text { get; set; }
    }

    public static class BookmlReader
    {
        private static readonly XNamespace Ns = XNamespace.Get("https://bookml.org/ns/1.0");

        /// <summary>
        /// Reads all prose paragraphs from a BookML chapter XML file, ordered by
        /// section seq then paragraph seq. Returns a flat list preserving that order.
        /// Text is extracted via ExtractText, which concatenates descendant text
        /// nodes (cleanly stripping inline markup like em/strong/footnote-ref) while
        /// excluding &lt;overlay&gt; text by default (diegetic on-screen text, not
        /// authorial prose -- amendment section 7.1) and preserving word boundaries
        /// at overlay/linebreak excision points.
        /// </summary>
        public static List<ParagraphEntry> ReadParagraphs(string chapterXmlPath, bool includeOverlays = false)
        {
            var doc = XDocument.Load(chapterXmlPath);
            var result = new List<ParagraphEntry>();

            var sections = doc.Root
                .Elements(Ns + "section")
                .OrderBy(s => ParseSeq(s));

            foreach (var section in sections)
            {
                var paras = section
                    .Elements(Ns + "para")
                    .OrderBy(p => ParseSeq(p));

                foreach (var para in paras)
                {
                    result.Add(new ParagraphEntry
                    {
                        Pid  = (string)para.Attribute("pid") ?? "",
                        Seq  = ParseSeq(para),
                        Text = ExtractText(para, includeOverlays)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Concatenates a paragraph's descendant text, excluding text inside
        /// &lt;overlay&gt; unless includeOverlays is true. A single space is
        /// inserted at each excluded-overlay or &lt;linebreak/&gt; boundary (if the
        /// buffer doesn't already end in whitespace) so adjacent words never fuse
        /// across the excision point -- e.g. "The banner flashed &lt;overlay&gt;Drink
        /// AguaPure&lt;/overlay&gt; and vanished." must not become "flashedand", which
        /// naive concatenation of the surviving text nodes would produce whenever
        /// the exporter emits no whitespace text node at the element boundary.
        /// Trailing/collapsed whitespace from these insertions is normalised away
        /// at the end. Classic type-check/cast rather than "is X x" pattern
        /// matching, to match this file's existing C# style on an old-style
        /// net462 .csproj with no explicit LangVersion set.
        /// </summary>
        private static string ExtractText(XElement para, bool includeOverlays)
        {
            var sb = new StringBuilder();
            foreach (var node in para.DescendantNodes())
            {
                var text = node as XText;
                if (text != null)
                {
                    bool inOverlay = text.Ancestors(Ns + "overlay").Any();
                    if (inOverlay && !includeOverlays)
                    {
                        if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]))
                            sb.Append(' ');
                        continue;
                    }
                    sb.Append(text.Value);
                    continue;
                }

                var el = node as XElement;
                if (el != null && el.Name == Ns + "linebreak")
                {
                    if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]))
                        sb.Append(' ');
                }
            }

            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>
        /// Returns the chapter id from the root element's id attribute.
        /// </summary>
        public static string ReadChapterId(string chapterXmlPath)
        {
            var doc = XDocument.Load(chapterXmlPath);
            return (string)doc.Root.Attribute("id") ?? "";
        }

        private static int ParseSeq(XElement el)
        {
            var attr = el.Attribute("seq");
            if (attr == null) return 0;
            int val;
            return int.TryParse(attr.Value, out val) ? val : 0;
        }
    }
}
