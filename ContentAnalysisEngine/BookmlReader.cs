using System.Collections.Generic;
using System.Linq;
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
        /// Text is extracted via XElement.Value which concatenates all descendant
        /// text nodes, cleanly stripping inline markup (em, strong, footnote-ref).
        /// </summary>
        public static List<ParagraphEntry> ReadParagraphs(string chapterXmlPath)
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
                        Text = para.Value
                    });
                }
            }

            return result;
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
