using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;

namespace Seonyx.Web.Services
{
    public class EpubExportResult
    {
        public bool         Success     { get; set; }
        public List<string> Warnings    { get; set; } = new List<string>();
        public string       EpubFileName { get; set; }
    }

    public class EpubExporter
    {
        // =====================================================================
        // PUBLIC API
        // =====================================================================

        public EpubExportResult Export(
            SeonyxContext       db,
            EpubConfigViewModel config,
            byte[]              coverImageBytes,
            string              coverMimeType,
            Stream              outputStream)
        {
            var result = new EpubExportResult();

            var project = db.BookProjects.Find(config.BookProjectID);
            if (project == null)
            {
                result.Warnings.Add("Project not found: " + config.BookProjectID);
                return result;
            }

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == config.BookProjectID)
                .OrderBy(c => c.SortOrder)
                .ToList();

            var bookUuid    = Guid.NewGuid().ToString();
            var hasCover    = coverImageBytes != null && coverImageBytes.Length > 0;
            var coverExt    = (coverMimeType == "image/png") ? "png" : "jpg";
            var exportDate  = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Collect chapter data from DB
            var chapterData = new List<ChapterData>();
            int fileIndex   = 1;
            foreach (var ch in chapters)
            {
                var data = BuildChapterData(db, ch, fileIndex, result);
                chapterData.Add(data);
                fileIndex++;
            }

            using (var zip = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
            {
                // mimetype MUST be first and uncompressed
                AddRawEntry(zip, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);

                // META-INF/container.xml
                AddTextEntry(zip, "META-INF/container.xml", BuildContainerXml());

                // OEBPS/content.opf
                AddTextEntry(zip, "OEBPS/content.opf",
                    BuildOpf(project, config, bookUuid, exportDate, chapterData, hasCover, coverExt));

                // OEBPS/nav.xhtml
                AddTextEntry(zip, "OEBPS/nav.xhtml",
                    BuildNavXhtml(project, chapterData));

                // OEBPS/toc.ncx
                AddTextEntry(zip, "OEBPS/toc.ncx",
                    BuildTocNcx(project, bookUuid, chapterData));

                // OEBPS/css/book.css
                AddTextEntry(zip, "OEBPS/css/book.css", BuildCss());

                // Cover files (optional)
                if (hasCover)
                {
                    AddTextEntry(zip, "OEBPS/cover.xhtml",
                        BuildCoverXhtml(project.ProjectName, coverExt));
                    AddBinaryEntry(zip, "OEBPS/images/cover." + coverExt, coverImageBytes);
                }

                // Copyright page
                AddTextEntry(zip, "OEBPS/copyright.xhtml",
                    BuildCopyrightXhtml(project.ProjectName, config));

                // Chapter and epigraph XHTML files
                foreach (var ch in chapterData)
                {
                    if (ch.Epigraphs.Count > 0)
                        AddTextEntry(zip, "OEBPS/" + ch.EpigraphFileName, BuildEpigraphXhtml(ch));
                    AddTextEntry(zip, "OEBPS/" + ch.FileName, BuildChapterXhtml(ch));
                }
            }

            result.Success      = true;
            result.EpubFileName = string.Format("{0}_{1}.epub",
                Slugify(project.ProjectName), DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            return result;
        }

        // =====================================================================
        // DB QUERY
        // =====================================================================

        private ChapterData BuildChapterData(
            SeonyxContext db, Chapter ch, int fileIndex, EpubExportResult result)
        {
            var data = new ChapterData
            {
                ChapterID      = ch.ChapterID,
                ChapterNumber  = ch.ChapterNumber,
                Title          = ch.ChapterTitle ?? string.Format("Chapter {0}", ch.ChapterNumber),
                FileName       = string.Format("ch{0:D3}.xhtml", fileIndex),
                NavId          = string.Format("ch{0:D3}", fileIndex),
                EpigraphFileName = string.Format("ch{0:D3}-epigraph.xhtml", fileIndex),
                EpigraphNavId  = string.Format("ch{0:D3}-epigraph", fileIndex)
            };

            // Working-copy paragraphs in display order
            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == ch.ChapterID)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            // Latest ParagraphVersion per pid (for ParaType and Seq)
            var latestVersionByPid = db.ParagraphVersions
                .Where(v => v.ChapterID == ch.ChapterID)
                .ToList()
                .GroupBy(v => v.Pid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.DraftNumber).First(),
                    StringComparer.OrdinalIgnoreCase);

            // Build paragraph rows, sorted by Seq where available
            var rows = new List<ParaRow>();
            int fallback = 0;
            foreach (var para in paragraphs)
            {
                fallback++;
                ParagraphVersion ver;
                latestVersionByPid.TryGetValue(para.UniqueID, out ver);

                int    seq      = (ver != null && ver.Seq > 0) ? ver.Seq : fallback * 1000;
                string paraType = (ver != null && !string.IsNullOrEmpty(ver.ParaType))
                    ? ver.ParaType
                    : "normal";

                rows.Add(new ParaRow
                {
                    Seq      = seq,
                    Pid      = para.UniqueID ?? "",
                    ParaType = paraType,
                    Text     = para.ParagraphText ?? ""
                });
            }

            // Sort by Seq ascending, then split epigraphs from body paragraphs.
            // Detect epigraphs by ParaType OR by PID prefix (-EP pattern e.g. CH02-EP001)
            // because AI-generated epigraphs may have been tagged type="normal" in the XML
            // while their PID encodes the correct element type.
            var sorted = rows.OrderBy(r => r.Seq).ToList();
            data.Epigraphs  = sorted.Where(r => IsEpigraph(r)).ToList();
            data.Paragraphs = sorted.Where(r => !IsEpigraph(r)).ToList();
            return data;
        }

        // =====================================================================
        // EPUB FILES
        // =====================================================================

        private static string BuildContainerXml()
        {
            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\r\n" +
"  <rootfiles>\r\n" +
"    <rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/>\r\n" +
"  </rootfiles>\r\n" +
"</container>";
        }

        private static string BuildOpf(
            BookProject         project,
            EpubConfigViewModel config,
            string              bookUuid,
            string              exportDate,
            List<ChapterData>   chapters,
            bool                hasCover,
            string              coverExt)
        {
            var manifest = new StringBuilder();
            var spine    = new StringBuilder();

            manifest.AppendLine("    <item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>");
            manifest.AppendLine("    <item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>");
            manifest.AppendLine("    <item id=\"css\" href=\"css/book.css\" media-type=\"text/css\"/>");

            if (hasCover)
            {
                manifest.AppendLine(string.Format(
                    "    <item id=\"cover-image\" href=\"images/cover.{0}\" media-type=\"{1}\" properties=\"cover-image\"/>",
                    coverExt, coverExt == "png" ? "image/png" : "image/jpeg"));
                manifest.AppendLine("    <item id=\"cover\" href=\"cover.xhtml\" media-type=\"application/xhtml+xml\"/>");
                spine.AppendLine("    <itemref idref=\"cover\" linear=\"yes\"/>");
            }

            manifest.AppendLine("    <item id=\"copyright\" href=\"copyright.xhtml\" media-type=\"application/xhtml+xml\"/>");
            spine.AppendLine("    <itemref idref=\"copyright\" linear=\"yes\"/>");

            foreach (var ch in chapters)
            {
                if (ch.Epigraphs.Count > 0)
                {
                    manifest.AppendLine(string.Format(
                        "    <item id=\"{0}\" href=\"{1}\" media-type=\"application/xhtml+xml\"/>",
                        ch.EpigraphNavId, ch.EpigraphFileName));
                    spine.AppendLine(string.Format("    <itemref idref=\"{0}\" linear=\"yes\"/>", ch.EpigraphNavId));
                }
                manifest.AppendLine(string.Format(
                    "    <item id=\"{0}\" href=\"{1}\" media-type=\"application/xhtml+xml\"/>",
                    ch.NavId, ch.FileName));
                spine.AppendLine(string.Format("    <itemref idref=\"{0}\" linear=\"yes\"/>", ch.NavId));
            }

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<package version=\"3.0\" xmlns=\"http://www.idpf.org/2007/opf\" unique-identifier=\"BookId\" xml:lang=\"en\">");
            sb.AppendLine("  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">");
            sb.AppendLine(string.Format("    <dc:identifier id=\"BookId\">urn:uuid:{0}</dc:identifier>", bookUuid));
            sb.AppendLine(string.Format("    <dc:title>{0}</dc:title>", HtmlEncode(project.ProjectName)));
            sb.AppendLine(string.Format("    <dc:creator>{0}</dc:creator>", HtmlEncode(config.RightsHolder)));
            sb.AppendLine("    <dc:language>en</dc:language>");
            sb.AppendLine(string.Format("    <dc:rights>Copyright {0} {1}. All rights reserved.</dc:rights>",
                config.CopyrightYear, HtmlEncode(config.RightsHolder)));
            sb.AppendLine(string.Format("    <meta property=\"dcterms:modified\">{0}T00:00:00Z</meta>", exportDate));
            if (hasCover)
                sb.AppendLine("    <meta name=\"cover\" content=\"cover-image\"/>");
            sb.AppendLine("  </metadata>");
            sb.AppendLine("  <manifest>");
            sb.Append(manifest);
            sb.AppendLine("  </manifest>");
            sb.AppendLine("  <spine toc=\"ncx\">");
            sb.Append(spine);
            sb.AppendLine("  </spine>");
            sb.AppendLine("</package>");
            return sb.ToString();
        }

        private static string BuildNavXhtml(BookProject project, List<ChapterData> chapters)
        {
            var toc = new StringBuilder();
            foreach (var ch in chapters)
            {
                toc.AppendLine(string.Format(
                    "      <li><a href=\"{0}\">{1}</a></li>",
                    ch.FileName, HtmlEncode(ch.Title)));
            }

            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<!DOCTYPE html>\r\n" +
"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"en\">\r\n" +
"<head>\r\n" +
"  <meta charset=\"UTF-8\"/>\r\n" +
"  <title>Table of Contents</title>\r\n" +
"</head>\r\n" +
"<body>\r\n" +
"  <nav epub:type=\"toc\" id=\"toc\">\r\n" +
"    <h1>Contents</h1>\r\n" +
"    <ol>\r\n" +
toc.ToString() +
"    </ol>\r\n" +
"  </nav>\r\n" +
"</body>\r\n" +
"</html>";
        }

        private static string BuildTocNcx(
            BookProject project, string bookUuid, List<ChapterData> chapters)
        {
            var navPoints = new StringBuilder();
            int playOrder = 1;
            foreach (var ch in chapters)
            {
                navPoints.AppendLine(string.Format(
                    "    <navPoint id=\"{0}\" playOrder=\"{1}\">\r\n" +
                    "      <navLabel><text>{2}</text></navLabel>\r\n" +
                    "      <content src=\"{3}\"/>\r\n" +
                    "    </navPoint>",
                    ch.NavId, playOrder++, HtmlEncode(ch.Title), ch.FileName));
            }

            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\r\n" +
"  <head>\r\n" +
string.Format("    <meta name=\"dtb:uid\" content=\"urn:uuid:{0}\"/>\r\n", bookUuid) +
"    <meta name=\"dtb:depth\" content=\"1\"/>\r\n" +
"    <meta name=\"dtb:totalPageCount\" content=\"0\"/>\r\n" +
"    <meta name=\"dtb:maxPageNumber\" content=\"0\"/>\r\n" +
"  </head>\r\n" +
string.Format("  <docTitle><text>{0}</text></docTitle>\r\n", HtmlEncode(project.ProjectName)) +
"  <navMap>\r\n" +
navPoints.ToString() +
"  </navMap>\r\n" +
"</ncx>";
        }

        private static string BuildCss()
        {
            return
"body {\r\n" +
"  font-family: Georgia, 'Times New Roman', serif;\r\n" +
"  font-size: 1em;\r\n" +
"  line-height: 1.4;\r\n" +
"  margin: 5% 8%;\r\n" +
"}\r\n" +
"\r\n" +
"p {\r\n" +
"  text-indent: 1.2em;\r\n" +
"  margin: 0 0 0.4em 0;\r\n" +
"  padding: 0;\r\n" +
"}\r\n" +
"\r\n" +
"p.thought {\r\n" +
"  font-style: italic;\r\n" +
"}\r\n" +
"\r\n" +
"p.extract,\r\n" +
"p.letter {\r\n" +
"  margin-left: 2em;\r\n" +
"  margin-right: 2em;\r\n" +
"  font-size: 0.95em;\r\n" +
"}\r\n" +
"\r\n" +
"p.verse {\r\n" +
"  white-space: pre-wrap;\r\n" +
"  margin-left: 2em;\r\n" +
"  text-indent: 0;\r\n" +
"}\r\n" +
"\r\n" +
"h1.chapter-title {\r\n" +
"  text-align: center;\r\n" +
"  margin-top: 3em;\r\n" +
"  margin-bottom: 1em;\r\n" +
"  font-size: 1.4em;\r\n" +
"}\r\n" +
"\r\n" +
"p.chapter-number {\r\n" +
"  text-align: center;\r\n" +
"  font-size: 0.9em;\r\n" +
"  letter-spacing: 0.1em;\r\n" +
"  text-indent: 0;\r\n" +
"  margin-top: 3em;\r\n" +
"}\r\n" +
"\r\n" +
"p.pullquote {\r\n" +
"  margin: 1em 2em;\r\n" +
"  font-style: italic;\r\n" +
"  text-indent: 0;\r\n" +
"}\r\n" +
"\r\n" +
"p.caption {\r\n" +
"  font-size: 0.85em;\r\n" +
"  text-align: center;\r\n" +
"  text-indent: 0;\r\n" +
"}\r\n" +
"\r\n" +
"div.cover-wrapper {\r\n" +
"  text-align: center;\r\n" +
"  margin: 0;\r\n" +
"  padding: 0;\r\n" +
"}\r\n" +
"\r\n" +
"div.cover-wrapper img {\r\n" +
"  max-width: 100%;\r\n" +
"  height: auto;\r\n" +
"}\r\n" +
"\r\n" +
"blockquote.epigraph {\r\n" +
"  margin: 4em 3em 2em 3em;\r\n" +
"  font-style: italic;\r\n" +
"  text-align: center;\r\n" +
"}\r\n" +
"\r\n" +
"blockquote.epigraph p {\r\n" +
"  text-indent: 0;\r\n" +
"  margin-bottom: 0.5em;\r\n" +
"}\r\n" +
"\r\n" +
"div.copyright-page {\r\n" +
"  margin-top: 4em;\r\n" +
"  font-size: 0.85em;\r\n" +
"  line-height: 1.6;\r\n" +
"}\r\n" +
"\r\n" +
"div.copyright-page p {\r\n" +
"  text-indent: 0;\r\n" +
"  margin-bottom: 0.5em;\r\n" +
"}\r\n";
        }

        private static string BuildCoverXhtml(string title, string coverExt)
        {
            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<!DOCTYPE html>\r\n" +
"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"en\">\r\n" +
"<head>\r\n" +
"  <meta charset=\"UTF-8\"/>\r\n" +
string.Format("  <title>{0}</title>\r\n", HtmlEncode(title)) +
"  <link rel=\"stylesheet\" type=\"text/css\" href=\"css/book.css\"/>\r\n" +
"</head>\r\n" +
"<body epub:type=\"cover\">\r\n" +
"  <div class=\"cover-wrapper\">\r\n" +
string.Format("    <img src=\"images/cover.{0}\" alt=\"Cover\"/>\r\n", coverExt) +
"  </div>\r\n" +
"</body>\r\n" +
"</html>";
        }

        private static string BuildCopyrightXhtml(
            string title, EpubConfigViewModel config)
        {
            var arcLine = config.ArcDisclaimer
                ? "\r\n    <p>This is an Advance Reader Copy (ARC). Not for resale or redistribution. " +
                  "The content may differ from the final published version.</p>"
                : "";

            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<!DOCTYPE html>\r\n" +
"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"en\">\r\n" +
"<head>\r\n" +
"  <meta charset=\"UTF-8\"/>\r\n" +
"  <title>Copyright</title>\r\n" +
"  <link rel=\"stylesheet\" type=\"text/css\" href=\"css/book.css\"/>\r\n" +
"</head>\r\n" +
"<body epub:type=\"copyright-page\">\r\n" +
"  <div class=\"copyright-page\">\r\n" +
string.Format("    <p><strong>{0}</strong></p>\r\n", HtmlEncode(title)) +
string.Format("    <p>Copyright &#169; {0} {1}. All rights reserved.</p>",
    config.CopyrightYear, HtmlEncode(config.RightsHolder)) +
arcLine + "\r\n" +
"  </div>\r\n" +
"</body>\r\n" +
"</html>";
        }

        private static string BuildEpigraphXhtml(ChapterData ch)
        {
            var body = new StringBuilder();
            body.AppendLine(string.Format(
                "  <section epub:type=\"epigraph\" id=\"{0}\">", ch.EpigraphNavId));
            foreach (var para in ch.Epigraphs)
            {
                body.AppendLine(string.Format("    <blockquote class=\"epigraph\">"));
                body.AppendLine(string.Format("      <p>{0}</p>", HtmlEncode(para.Text)));
                body.AppendLine("    </blockquote>");
            }
            body.AppendLine("  </section>");

            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<!DOCTYPE html>\r\n" +
"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"en\">\r\n" +
"<head>\r\n" +
"  <meta charset=\"UTF-8\"/>\r\n" +
string.Format("  <title>Epigraph - {0}</title>\r\n", HtmlEncode(ch.Title)) +
"  <link rel=\"stylesheet\" type=\"text/css\" href=\"css/book.css\"/>\r\n" +
"</head>\r\n" +
"<body>\r\n" +
body.ToString() +
"</body>\r\n" +
"</html>";
        }

        private static string BuildChapterXhtml(ChapterData ch)
        {
            var body = new StringBuilder();
            body.AppendLine(string.Format(
                "  <section epub:type=\"chapter\" id=\"{0}\">", ch.NavId));
            body.AppendLine(string.Format(
                "    <h1 class=\"chapter-title\">{0}</h1>", HtmlEncode(ch.Title)));

            foreach (var para in ch.Paragraphs)
            {
                body.AppendLine(RenderPara(para));
            }

            body.AppendLine("  </section>");

            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
"<!DOCTYPE html>\r\n" +
"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"en\">\r\n" +
"<head>\r\n" +
"  <meta charset=\"UTF-8\"/>\r\n" +
string.Format("  <title>{0}</title>\r\n", HtmlEncode(ch.Title)) +
"  <link rel=\"stylesheet\" type=\"text/css\" href=\"css/book.css\"/>\r\n" +
"</head>\r\n" +
"<body>\r\n" +
body.ToString() +
"</body>\r\n" +
"</html>";
        }

        private static string RenderPara(ParaRow para)
        {
            var text = HtmlEncode(para.Text);
            switch (para.ParaType)
            {
                case "normal":
                case "dialogue":
                case "thought":
                case "letter":
                case "extract":
                case "verse":
                case "caption":
                case "pullquote":
                    if (para.ParaType == "normal")
                        return string.Format("    <p>{0}</p>", text);
                    return string.Format("    <p class=\"{0}\">{1}</p>", para.ParaType, text);
                default:
                    return string.Format("    <!-- unknown paraType: {0} -->", para.ParaType);
            }
        }

        // =====================================================================
        // ZIP HELPERS
        // =====================================================================

        private static void AddRawEntry(
            ZipArchive zip, string name, string content, CompressionLevel level)
        {
            var entry = zip.CreateEntry(name, level);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static void AddTextEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static void AddBinaryEntry(ZipArchive zip, string name, byte[] data)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                stream.Write(data, 0, data.Length);
            }
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        // A paragraph is an epigraph if its ParaType is "epigraph" OR if its PID
        // follows the BookML EP type-code pattern (e.g. CH02-EP001).
        private static readonly System.Text.RegularExpressions.Regex EpigraphPidPattern =
            new System.Text.RegularExpressions.Regex(
                @"^[A-Z][A-Z0-9]*-EP\d+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static bool IsEpigraph(ParaRow row)
        {
            if (row.ParaType == "epigraph") return true;
            if (!string.IsNullOrEmpty(row.Pid) && EpigraphPidPattern.IsMatch(row.Pid)) return true;
            return false;
        }

        private static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return WebUtility.HtmlEncode(s);
        }

        private static string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "ebook";
            var slug = System.Text.RegularExpressions.Regex.Replace(
                text.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            return slug.Trim('-');
        }

        // =====================================================================
        // DATA STRUCTURES
        // =====================================================================

        private class ChapterData
        {
            public int           ChapterID        { get; set; }
            public int           ChapterNumber    { get; set; }
            public string        Title            { get; set; }
            public string        FileName         { get; set; }
            public string        NavId            { get; set; }
            public string        EpigraphFileName { get; set; }
            public string        EpigraphNavId    { get; set; }
            public List<ParaRow> Paragraphs       { get; set; } = new List<ParaRow>();
            public List<ParaRow> Epigraphs        { get; set; } = new List<ParaRow>();
        }

        private class ParaRow
        {
            public int    Seq      { get; set; }
            public string Pid      { get; set; }
            public string ParaType { get; set; }
            public string Text     { get; set; }
        }
    }
}
