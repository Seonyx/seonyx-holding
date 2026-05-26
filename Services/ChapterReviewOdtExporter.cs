using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Seonyx.Web.Models;

namespace Seonyx.Web.Services
{
    public class ChapterReviewOdtExporter
    {
        // OpenDocument namespace URIs
        private static readonly XNamespace NsOffice   = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private static readonly XNamespace NsStyle    = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        private static readonly XNamespace NsText     = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private static readonly XNamespace NsFo       = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        private static readonly XNamespace NsMeta     = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
        private static readonly XNamespace NsDc       = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace NsManifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

        // Non-breaking space (U+00A0) separating PID label from paragraph text
        private const string Nbsp = " ";

        /// <summary>
        /// Exports a single chapter as a double-spaced ODT file for editorial review.
        /// Paragraphs are ordered by OrdinalPosition. The Pid label is looked up from
        /// ParagraphVersions and used only as an identifier, never as an ordering key.
        /// </summary>
        /// <returns>Raw bytes of the .odt file.</returns>
        public byte[] Export(SeonyxContext db, int chapterId, out string fileName)
        {
            var chapter = db.Chapters.Find(chapterId);
            if (chapter == null)
                throw new ArgumentException("Chapter not found: " + chapterId);

            var project = db.BookProjects.Find(chapter.BookProjectID);
            string bookTitle    = project != null ? project.ProjectName : "Book";
            string chapterTitle = chapter.ChapterTitle ?? ("Chapter " + chapter.ChapterNumber);

            // Load paragraphs in display order. OrdinalPosition is the sole ordering key.
            // Do NOT sort by Pid — Pid is an identity label, not an ordinal.
            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == chapterId)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            // Build a Pid -> latest Pid lookup keyed by ParagraphVersion.Pid,
            // which equals Paragraph.UniqueID (confirmed by BookmlExporter pattern).
            // Used ONLY to look up the label for each paragraph; never iterated for output order.
            var uniqueIdToPid = db.ParagraphVersions
                .Where(v => v.ChapterID == chapterId)
                .ToList()
                .GroupBy(v => v.Pid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.DraftNumber).First().Pid,
                    StringComparer.OrdinalIgnoreCase);

            fileName = string.Format("{0}-{1}-review.odt",
                Slugify(bookTitle), Slugify(chapterTitle));

            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    // Rule: mimetype MUST be first entry, stored (not deflated), no extra fields.
                    WriteMimetype(zip);
                    WriteManifest(zip);
                    WriteStyles(zip);
                    WriteContent(zip, chapterTitle, paragraphs, uniqueIdToPid);
                    WriteMeta(zip, chapterTitle, bookTitle, DateTime.UtcNow);
                }
                return ms.ToArray();
            }
        }

        private static void WriteMimetype(ZipArchive zip)
        {
            // Must be first, stored (CompressionLevel.NoCompression), no trailing newline.
            var entry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var sw = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                sw.Write("application/vnd.oasis.opendocument.text");
        }

        private static void WriteManifest(ZipArchive zip)
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(NsManifest + "manifest",
                    new XAttribute(XNamespace.Xmlns + "manifest", NsManifest.NamespaceName),
                    new XAttribute(NsManifest + "version", "1.3"),
                    new XElement(NsManifest + "file-entry",
                        new XAttribute(NsManifest + "full-path", "/"),
                        new XAttribute(NsManifest + "media-type", "application/vnd.oasis.opendocument.text"),
                        new XAttribute(NsManifest + "version", "1.3")),
                    new XElement(NsManifest + "file-entry",
                        new XAttribute(NsManifest + "full-path", "content.xml"),
                        new XAttribute(NsManifest + "media-type", "text/xml")),
                    new XElement(NsManifest + "file-entry",
                        new XAttribute(NsManifest + "full-path", "styles.xml"),
                        new XAttribute(NsManifest + "media-type", "text/xml")),
                    new XElement(NsManifest + "file-entry",
                        new XAttribute(NsManifest + "full-path", "meta.xml"),
                        new XAttribute(NsManifest + "media-type", "text/xml"))
                )
            );
            WriteXmlEntry(zip, "META-INF/manifest.xml", doc);
        }

        private static void WriteStyles(ZipArchive zip)
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(NsOffice + "document-styles",
                    new XAttribute(XNamespace.Xmlns + "office", NsOffice.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "style",  NsStyle.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "text",   NsText.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "fo",     NsFo.NamespaceName),
                    new XAttribute(NsOffice + "version", "1.3"),

                    new XElement(NsOffice + "styles",
                        // Body paragraph style: double-spaced, first-line indent, no inter-para space
                        new XElement(NsStyle + "style",
                            new XAttribute(NsStyle + "name", "ReviewBody"),
                            new XAttribute(NsStyle + "family", "paragraph"),
                            new XElement(NsStyle + "paragraph-properties",
                                new XAttribute(NsFo + "line-height",   "200%"),
                                new XAttribute(NsFo + "text-indent",   "0.5in"),
                                new XAttribute(NsFo + "margin-top",    "0in"),
                                new XAttribute(NsFo + "margin-bottom", "0in")),
                            new XElement(NsStyle + "text-properties",
                                new XAttribute(NsFo + "font-family", "Times New Roman"),
                                new XAttribute(NsFo + "font-size",   "12pt"))),

                        // Character style for PID label: grey Courier New 9pt
                        new XElement(NsStyle + "style",
                            new XAttribute(NsStyle + "name", "PidLabel"),
                            new XAttribute(NsStyle + "family", "text"),
                            new XElement(NsStyle + "text-properties",
                                new XAttribute(NsFo + "font-family", "Courier New"),
                                new XAttribute(NsFo + "font-size",   "9pt"),
                                new XAttribute(NsFo + "color",       "#666666"))),

                        // Chapter title style: centred, bold, 14pt, bottom margin
                        new XElement(NsStyle + "style",
                            new XAttribute(NsStyle + "name", "ChapterTitle"),
                            new XAttribute(NsStyle + "family", "paragraph"),
                            new XElement(NsStyle + "paragraph-properties",
                                new XAttribute(NsFo + "text-align",   "center"),
                                new XAttribute(NsFo + "margin-bottom","0.5in")),
                            new XElement(NsStyle + "text-properties",
                                new XAttribute(NsFo + "font-family",  "Times New Roman"),
                                new XAttribute(NsFo + "font-size",    "14pt"),
                                new XAttribute(NsFo + "font-weight",  "bold")))
                    ),

                    new XElement(NsOffice + "automatic-styles",
                        new XElement(NsStyle + "page-layout",
                            new XAttribute(NsStyle + "name", "ReviewPage"),
                            new XElement(NsStyle + "page-layout-properties",
                                new XAttribute(NsFo + "page-width",    "8.27in"),
                                new XAttribute(NsFo + "page-height",   "11.69in"),
                                new XAttribute(NsFo + "margin-top",    "1.5in"),
                                new XAttribute(NsFo + "margin-bottom", "1.5in"),
                                new XAttribute(NsFo + "margin-left",   "1.5in"),
                                new XAttribute(NsFo + "margin-right",  "1.5in")))
                    ),

                    new XElement(NsOffice + "master-styles",
                        new XElement(NsStyle + "master-page",
                            new XAttribute(NsStyle + "name", "Standard"),
                            new XAttribute(NsStyle + "page-layout-name", "ReviewPage")))
                )
            );
            WriteXmlEntry(zip, "styles.xml", doc);
        }

        private static void WriteContent(ZipArchive zip, string chapterTitle,
            System.Collections.Generic.List<Models.Paragraph> paragraphs,
            System.Collections.Generic.Dictionary<string, string> uniqueIdToPid)
        {
            var body = new XElement(NsOffice + "text",
                new XElement(NsText + "p",
                    new XAttribute(NsText + "style-name", "ChapterTitle"),
                    new XText(chapterTitle ?? ""))
            );

            // Iterate paragraphs in OrdinalPosition order (already sorted by the DB query).
            // Pid is looked up by UniqueID for the label only — it is not used for ordering.
            foreach (var para in paragraphs)
            {
                string pid;
                if (!uniqueIdToPid.TryGetValue(para.UniqueID, out pid))
                    pid = para.UniqueID;

                string label = "[" + pid + "]";

                var paraEl = new XElement(NsText + "p",
                    new XAttribute(NsText + "style-name", "ReviewBody"),
                    new XElement(NsText + "span",
                        new XAttribute(NsText + "style-name", "PidLabel"),
                        new XText(label + Nbsp)),
                    new XText(para.ParagraphText ?? "")
                );
                body.Add(paraEl);
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(NsOffice + "document-content",
                    new XAttribute(XNamespace.Xmlns + "office", NsOffice.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "text",   NsText.NamespaceName),
                    new XAttribute(NsOffice + "version", "1.3"),
                    new XElement(NsOffice + "body", body)
                )
            );
            WriteXmlEntry(zip, "content.xml", doc);
        }

        private static void WriteMeta(ZipArchive zip, string chapterTitle,
            string bookTitle, DateTime generatedUtc)
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(NsOffice + "document-meta",
                    new XAttribute(XNamespace.Xmlns + "office", NsOffice.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "meta",   NsMeta.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "dc",     NsDc.NamespaceName),
                    new XAttribute(NsOffice + "version", "1.3"),
                    new XElement(NsOffice + "meta",
                        new XElement(NsMeta + "generator", "Seonyx Book Editor"),
                        new XElement(NsDc + "title", chapterTitle ?? ""),
                        new XElement(NsDc + "description", "Editorial review export — " + bookTitle),
                        new XElement(NsMeta + "creation-date",
                            generatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    )
                )
            );
            WriteXmlEntry(zip, "meta.xml", doc);
        }

        private static void WriteXmlEntry(ZipArchive zip, string entryName, XDocument doc)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding    = new UTF8Encoding(false),
                Indent      = true,
                IndentChars = "  "
            }))
            {
                doc.Save(writer);
            }
        }

        private static string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "untitled";
            var slug = text.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? "untitled" : slug;
        }
    }
}
