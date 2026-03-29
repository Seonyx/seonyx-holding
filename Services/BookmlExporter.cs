using System;
using System.Collections.Generic;
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
    public class BookmlExportResult
    {
        public bool           Success  { get; set; }
        public List<string>   Warnings { get; set; } = new List<string>();
        public string         ZipFileName { get; set; }
    }

    /// <summary>
    /// Exports the working copy of a BookProject as a BookML ZIP package.
    /// All para elements are guaranteed to carry a seq attribute.
    /// Paragraphs whose working-copy text differs from the last imported
    /// ParagraphVersion are marked draft-modified=currentDraft, modified-by=human.
    /// </summary>
    public class BookmlExporter
    {
        private static readonly XNamespace Ns = "https://bookml.org/ns/1.0";
        private const string BookmlVersion = "1.0";

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        public BookmlExportResult Export(SeonyxContext db, int projectId, Stream outputStream)
        {
            var result = new BookmlExportResult();

            var project = db.BookProjects.Find(projectId);
            if (project == null)
            {
                result.Warnings.Add("Project not found: " + projectId);
                return result;
            }

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == projectId)
                .OrderBy(c => c.ChapterNumber)
                .ToList();

            var drafts = db.Drafts
                .Where(d => d.BookProjectID == projectId)
                .OrderBy(d => d.DraftNumber)
                .ToList();

            var bookId = !string.IsNullOrEmpty(project.BookmlId)
                ? project.BookmlId
                : Slugify(project.ProjectName);

            var exportDate = DateTime.UtcNow;
            var components = new List<ComponentInfo>();

            using (var zip = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
            {
                int compSeq = 1000;
                foreach (var ch in chapters)
                {
                    var chId      = GetChapterId(ch);
                    var chIdLower = chId.ToLowerInvariant();

                    components.Add(new ComponentInfo
                    {
                        Id          = chId,
                        SeqValue    = compSeq,
                        Title       = ch.ChapterTitle ?? string.Format("Chapter {0}", ch.ChapterNumber),
                        ChapterFile = string.Format("{0}/{0}-chapter.xml", chIdLower),
                        MetaFile    = string.Format("{0}/{0}-meta.xml",    chIdLower),
                        NotesFile   = string.Format("{0}/{0}-notes.xml",   chIdLower),
                        DraftNumber = project.CurrentDraftNumber
                    });
                    compSeq += 1000;

                    // Working-copy paragraphs, display order
                    var paragraphs = db.Paragraphs
                        .Where(p => p.ChapterID == ch.ChapterID)
                        .OrderBy(p => p.OrdinalPosition)
                        .ToList();

                    // Latest ParagraphVersion per pid (for seq, provenance)
                    var latestVersionByPid = db.ParagraphVersions
                        .Where(v => v.ChapterID == ch.ChapterID)
                        .ToList()
                        .GroupBy(v => v.Pid, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(v => v.DraftNumber).First(),
                            StringComparer.OrdinalIgnoreCase);

                    var paraIds = paragraphs.Select(p => p.ParagraphID).ToList();

                    var metaByParaId = db.MetaNotes
                        .Where(m => paraIds.Contains(m.ParagraphID))
                        .ToList()
                        .ToDictionary(m => m.ParagraphID);

                    var noteByParaId = db.EditNotes
                        .Where(n => paraIds.Contains(n.ParagraphID))
                        .ToList()
                        .ToDictionary(n => n.ParagraphID);

                    AddXmlEntry(zip, string.Format("{0}/{0}-chapter.xml", chIdLower),
                        BuildChapterXml(ch, chId, bookId, project.CurrentDraftNumber,
                            paragraphs, latestVersionByPid, result));

                    AddXmlEntry(zip, string.Format("{0}/{0}-meta.xml", chIdLower),
                        BuildMetaXml(ch, chId, bookId, project.CurrentDraftNumber,
                            paragraphs, metaByParaId));

                    AddXmlEntry(zip, string.Format("{0}/{0}-notes.xml", chIdLower),
                        BuildNotesXml(ch, chId, bookId, project.CurrentDraftNumber,
                            paragraphs, noteByParaId, exportDate));
                }

                AddXmlEntry(zip, "book.xml",
                    BuildBookXml(project, bookId, drafts, components, exportDate));
            }

            result.Success     = true;
            result.ZipFileName = string.Format("{0}_export_{1}.bookml.zip",
                bookId, exportDate.ToString("yyyyMMdd_HHmmss"));
            return result;
        }

        // =====================================================================
        // BOOK.XML
        // =====================================================================

        private XDocument BuildBookXml(BookProject project, string bookId,
            List<Draft> drafts, List<ComponentInfo> components, DateTime exportDate)
        {
            var versioningEl = new XElement(Ns + "versioning");
            if (drafts.Any())
            {
                foreach (var d in drafts.OrderBy(d => d.DraftNumber))
                {
                    // The current draft is being exported as a finished snapshot;
                    // mark it "snapshot" regardless of its DB status.
                    var exportStatus = (d.DraftNumber == project.CurrentDraftNumber)
                        ? "snapshot"
                        : d.Status;
                    versioningEl.Add(new XElement(Ns + "draft",
                        new XAttribute("number",      d.DraftNumber),
                        new XAttribute("status",      exportStatus),
                        new XAttribute("created",     d.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XAttribute("based-on",    d.BasedOn),
                        new XAttribute("author-type", d.AuthorType),
                        new XAttribute("author",      d.Author ?? ""),
                        new XAttribute("label",       d.Label ?? "")
                    ));
                }
            }
            else
            {
                versioningEl.Add(new XElement(Ns + "draft",
                    new XAttribute("number",      project.CurrentDraftNumber),
                    new XAttribute("status",      "in-progress"),
                    new XAttribute("created",     exportDate.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                    new XAttribute("based-on",    0),
                    new XAttribute("author-type", "human"),
                    new XAttribute("author",      ""),
                    new XAttribute("label",       "Export")
                ));
            }

            var bodymatterEl = new XElement(Ns + "bodymatter");
            foreach (var comp in components)
            {
                bodymatterEl.Add(new XElement(Ns + "component",
                    new XAttribute("id",           comp.Id),
                    new XAttribute("type",         "chapter"),
                    new XAttribute("seq",          comp.SeqValue),
                    new XAttribute("chapter-file", comp.ChapterFile),
                    new XAttribute("meta-file",    comp.MetaFile),
                    new XAttribute("notes-file",   comp.NotesFile),
                    new XAttribute("title",        comp.Title),
                    new XAttribute("draft",        comp.DraftNumber)
                ));
            }

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(Ns + "book",
                    new XAttribute("bookml-version", BookmlVersion),
                    new XAttribute("id",             bookId),
                    new XElement(Ns + "bookinfo",
                        new XElement(Ns + "title",  project.ProjectName),
                        new XElement(Ns + "author", new XAttribute("role", "author"),
                            new XElement(Ns + "surname", "")),
                        new XElement(Ns + "genre",    "fiction"),
                        new XElement(Ns + "language", "en"),
                        versioningEl,
                        new XElement(Ns + "created",  exportDate.ToString("yyyy-MM-dd")),
                        new XElement(Ns + "modified", exportDate.ToString("yyyy-MM-dd"))
                    ),
                    new XElement(Ns + "contents", bodymatterEl)
                )
            );
        }

        // =====================================================================
        // CHAPTER XML — seq is mandatory on every para
        // =====================================================================

        private XDocument BuildChapterXml(Chapter ch, string chId, string bookId,
            int draftNumber, List<Paragraph> paragraphs,
            Dictionary<string, ParagraphVersion> latestVersionByPid,
            BookmlExportResult result)
        {
            var sectionEl = new XElement(Ns + "section", new XAttribute("seq", 1000));

            int fallbackOrdinal = 0;
            foreach (var para in paragraphs)
            {
                fallbackOrdinal++;

                ParagraphVersion ver;
                latestVersionByPid.TryGetValue(para.UniqueID, out ver);

                int      seq;
                string   paraType;
                int      draftCreated;
                int      draftModified;
                string   modifiedBy;
                DateTime? modifiedDate;

                if (ver != null)
                {
                    // seq: use stored value; fall back to position*1000 if missing
                    if (ver.Seq > 0)
                    {
                        seq = ver.Seq;
                    }
                    else
                    {
                        seq = fallbackOrdinal * 1000;
                        result.Warnings.Add(string.Format(
                            "pid {0}: stored seq is 0, using fallback {1}",
                            para.UniqueID, seq));
                    }

                    paraType     = string.IsNullOrEmpty(ver.ParaType) ? "normal" : ver.ParaType;
                    draftCreated = ver.DraftCreated;

                    // Detect human edits: working-copy text differs from last import
                    bool editedByHuman = !string.Equals(
                        (para.ParagraphText ?? "").Trim(),
                        (ver.Content ?? "").Trim(),
                        StringComparison.Ordinal);

                    if (editedByHuman)
                    {
                        draftModified = draftNumber;
                        modifiedBy    = "human";
                        modifiedDate  = para.LastModifiedDate;
                    }
                    else
                    {
                        draftModified = ver.DraftModified;
                        modifiedBy    = string.IsNullOrEmpty(ver.ModifiedBy) ? "ai" : ver.ModifiedBy;
                        modifiedDate  = ver.ModifiedDate;
                    }
                }
                else
                {
                    // No version record — paragraph created in editor, never imported
                    seq           = fallbackOrdinal * 1000;
                    paraType      = "normal";
                    draftCreated  = draftNumber;
                    draftModified = draftNumber;
                    modifiedBy    = "human";
                    modifiedDate  = para.LastModifiedDate;
                    result.Warnings.Add(string.Format(
                        "pid {0}: no ParagraphVersion, using fallback seq {1}",
                        para.UniqueID, seq));
                }

                var paraEl = new XElement(Ns + "para",
                    new XAttribute("pid",            para.UniqueID),
                    new XAttribute("seq",            seq),
                    new XAttribute("type",           paraType),
                    new XAttribute("draft-created",  draftCreated),
                    new XAttribute("draft-modified", draftModified),
                    new XAttribute("modified-by",    modifiedBy)
                );

                if (modifiedDate.HasValue)
                    paraEl.Add(new XAttribute("modified-date",
                        modifiedDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")));

                paraEl.Value = para.ParagraphText ?? "";
                sectionEl.Add(paraEl);
            }

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(Ns + "chapter",
                    new XAttribute("bookml-version", BookmlVersion),
                    new XAttribute("id",             chId),
                    new XAttribute("book-id",        bookId),
                    new XAttribute("draft",          draftNumber),
                    new XElement(Ns + "chapterinfo",
                        new XElement(Ns + "chapternumber", ch.ChapterNumber),
                        new XElement(Ns + "title", ch.ChapterTitle ?? "")
                    ),
                    sectionEl
                )
            );
        }

        // =====================================================================
        // META XML
        // =====================================================================

        private XDocument BuildMetaXml(Chapter ch, string chId, string bookId,
            int draftNumber, List<Paragraph> paragraphs,
            Dictionary<int, MetaNote> metaByParaId)
        {
            var metaEl = new XElement(Ns + "meta",
                new XAttribute("bookml-version", BookmlVersion),
                new XAttribute("chapter-id",     chId),
                new XAttribute("book-id",        bookId),
                new XAttribute("draft",          draftNumber)
            );

            foreach (var para in paragraphs)
            {
                MetaNote meta;
                if (!metaByParaId.TryGetValue(para.ParagraphID, out meta)) continue;
                if (string.IsNullOrWhiteSpace(meta.MetaText)) continue;

                metaEl.Add(new XElement(Ns + "para-meta",
                    new XAttribute("pid", para.UniqueID),
                    new XElement(Ns + "tags",
                        new XElement(Ns + "tag", meta.MetaText)
                    )
                ));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", null), metaEl);
        }

        // =====================================================================
        // NOTES XML
        // =====================================================================

        private XDocument BuildNotesXml(Chapter ch, string chId, string bookId,
            int draftNumber, List<Paragraph> paragraphs,
            Dictionary<int, EditNote> noteByParaId, DateTime exportDate)
        {
            var notesEl = new XElement(Ns + "notes",
                new XAttribute("bookml-version", BookmlVersion),
                new XAttribute("chapter-id",     chId),
                new XAttribute("book-id",        bookId),
                new XAttribute("draft",          draftNumber)
            );

            foreach (var para in paragraphs)
            {
                EditNote note;
                if (!noteByParaId.TryGetValue(para.ParagraphID, out note)) continue;
                if (string.IsNullOrWhiteSpace(note.NoteText)) continue;

                var noteId = string.Format("{0}-N001", para.UniqueID);
                notesEl.Add(new XElement(Ns + "para-notes",
                    new XAttribute("pid", para.UniqueID),
                    new XElement(Ns + "note",
                        new XAttribute("id",          noteId),
                        new XAttribute("type",        "craft"),
                        new XAttribute("author-type", "human"),
                        new XAttribute("author",      ""),
                        new XAttribute("draft",       draftNumber),
                        new XAttribute("created",     exportDate.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XElement(Ns + "body", note.NoteText)
                    )
                ));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", null), notesEl);
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static string GetChapterId(Chapter ch)
        {
            if (!string.IsNullOrEmpty(ch.BookmlChapterId))
                return ch.BookmlChapterId.ToUpperInvariant();
            return string.Format("CH{0:D2}", ch.ChapterNumber);
        }

        private static void AddXmlEntry(ZipArchive zip, string entryName, XDocument doc)
        {
            var entry = zip.CreateEntry(entryName);
            using (var stream = entry.Open())
            {
                var settings = new XmlWriterSettings
                {
                    Encoding    = new UTF8Encoding(false), // UTF-8, no BOM
                    Indent      = true,
                    IndentChars = "  "
                };
                using (var writer = XmlWriter.Create(stream, settings))
                {
                    doc.WriteTo(writer);
                }
            }
        }

        private static string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "project";
            var slug = text.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? "project" : slug;
        }

        private class ComponentInfo
        {
            public string Id          { get; set; }
            public int    SeqValue    { get; set; }
            public string Title       { get; set; }
            public string ChapterFile { get; set; }
            public string MetaFile    { get; set; }
            public string NotesFile   { get; set; }
            public int    DraftNumber { get; set; }
        }
    }
}
