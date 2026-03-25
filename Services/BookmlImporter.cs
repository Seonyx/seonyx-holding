using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Seonyx.Web.Models;

namespace Seonyx.Web.Services
{
    public class ImportResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string BookTitle { get; set; }
        public int DraftNumber { get; set; }
        public int ChaptersProcessed { get; set; }
        public int ParagraphsAdded { get; set; }
        public int ParagraphsUpdated { get; set; }
        public int ParagraphsRemoved { get; set; }
        public int VersionsRecorded { get; set; }

        public string Summary()
        {
            var parts = new List<string>();
            parts.Add(string.Format("Draft {0}", DraftNumber));
            parts.Add(string.Format("{0} chapter(s)", ChaptersProcessed));
            if (ParagraphsAdded > 0)   parts.Add(string.Format("{0} added", ParagraphsAdded));
            if (ParagraphsUpdated > 0) parts.Add(string.Format("{0} updated", ParagraphsUpdated));
            if (ParagraphsRemoved > 0) parts.Add(string.Format("{0} removed from working copy", ParagraphsRemoved));
            parts.Add(string.Format("{0} versions recorded", VersionsRecorded));
            return "Import complete: " + string.Join(", ", parts) + ".";
        }
    }

    public class BookmlImporter
    {
        private static readonly XNamespace Ns = "https://bookml.org/ns/1.0";
        private readonly string _xsdBasePath;
        private XmlSchemaSet _schemas;

        public BookmlImporter(string xsdBasePath)
        {
            _xsdBasePath = xsdBasePath;
        }

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        /// <summary>
        /// Phase 1: validate all XML files against XSD and check pid integrity.
        /// Returns a list of error strings; an empty list means the package is valid.
        /// DOES NOT touch the database.
        /// </summary>
        public List<string> Validate(string bookXmlPath)
        {
            var errors = new List<string>();

            if (!File.Exists(bookXmlPath))
            {
                errors.Add("book.xml not found at: " + bookXmlPath);
                return errors;
            }

            try
            {
                _schemas = LoadSchemaSet();
            }
            catch (Exception ex)
            {
                errors.Add("Failed to load BookML XSD schemas from " + _xsdBasePath + ": " + ex.Message);
                return errors;
            }

            // Validate book.xml first — can't resolve component paths without it
            errors.AddRange(ValidateXml(bookXmlPath));
            if (errors.Any()) return errors;

            var bookDir = Path.GetDirectoryName(bookXmlPath);
            List<ComponentEntry> components;
            try
            {
                components = ParseComponents(bookXmlPath, bookDir);
            }
            catch (Exception ex)
            {
                errors.Add("Failed to parse book.xml contents list: " + ex.Message);
                return errors;
            }

            if (components.Count == 0)
            {
                errors.Add("book.xml contains no components in bodymatter.");
                return errors;
            }

            foreach (var comp in components)
            {
                if (!File.Exists(comp.ChapterPath))
                {
                    errors.Add(string.Format("Chapter file not found: {0}", comp.ChapterFile));
                    continue;
                }
                if (!File.Exists(comp.MetaPath))
                {
                    errors.Add(string.Format("Meta file not found: {0}", comp.MetaFile));
                    continue;
                }
                if (!File.Exists(comp.NotesPath))
                {
                    errors.Add(string.Format("Notes file not found: {0}", comp.NotesFile));
                    continue;
                }

                var chapterErrors = ValidateXml(comp.ChapterPath);
                var metaErrors    = ValidateXml(comp.MetaPath);
                var notesErrors   = ValidateXml(comp.NotesPath);

                errors.AddRange(chapterErrors);
                errors.AddRange(metaErrors);
                errors.AddRange(notesErrors);

                // Pid integrity check only makes sense if all three files are schema-valid
                if (!chapterErrors.Any() && !metaErrors.Any() && !notesErrors.Any())
                {
                    var chapterPids = ExtractPidsFromChapter(comp.ChapterPath);
                    errors.AddRange(CheckPidIntegrity(comp.MetaPath,  "meta",  chapterPids, comp.Id));
                    errors.AddRange(CheckPidIntegrity(comp.NotesPath, "notes", chapterPids, comp.Id));
                }
            }

            return errors;
        }

        /// <summary>
        /// Phase 2: import a BookML package into the database.
        /// Re-validates defensively before any DB write — rejects the entire import if
        /// anything fails. Never overwrites existing ParagraphVersions rows.
        /// </summary>
        public ImportResult Import(SeonyxContext db, int projectId, string bookXmlPath,
            Action<int, int, string, int, int> onChapterImported = null)
        {
            var result = new ImportResult();

            // Defensive re-validation: no DB writes if the package is not clean
            var validationErrors = Validate(bookXmlPath);
            if (validationErrors.Any())
            {
                result.Success = false;
                result.Errors.AddRange(validationErrors);
                return result;
            }

            var project = db.BookProjects.Find(projectId);
            if (project == null)
            {
                result.Success = false;
                result.Errors.Add("Project not found (id=" + projectId + ").");
                return result;
            }

            var bookDir = Path.GetDirectoryName(bookXmlPath);
            var bookDoc = XDocument.Load(bookXmlPath);

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var bookInfoEl    = bookDoc.Root.Element(Ns + "bookinfo");
                    result.BookTitle  = bookInfoEl.Element(Ns + "title")?.Value ?? "";
                    var bookId        = bookDoc.Root.Attribute("id")?.Value ?? "";

                    // ----------------------------------------------------------
                    // Insert draft lineage records (append-only: never overwrite)
                    // ----------------------------------------------------------
                    int importDraftNumber = 0;
                    var versioningEl = bookInfoEl.Element(Ns + "versioning");

                    foreach (var draftEl in versioningEl.Elements(Ns + "draft"))
                    {
                        var draftNum    = int.Parse(draftEl.Attribute("number").Value);
                        var draftStatus = draftEl.Attribute("status").Value;

                        var existing = db.Drafts.FirstOrDefault(
                            d => d.BookProjectID == projectId && d.DraftNumber == draftNum);

                        if (existing == null)
                        {
                            db.Drafts.Add(new Draft
                            {
                                BookProjectID = projectId,
                                DraftNumber   = draftNum,
                                Status        = draftStatus,
                                AuthorType    = draftEl.Attribute("author-type").Value,
                                Author        = draftEl.Attribute("author").Value,
                                BasedOn       = ParseInt(draftEl.Attribute("based-on")?.Value, 0),
                                Label         = draftEl.Attribute("label")?.Value,
                                DraftNote     = draftEl.Element(Ns + "note")?.Value,
                                CreatedDate   = ParseDateTime(draftEl.Attribute("created")?.Value)
                            });
                        }
                        // else: existing draft record — never overwrite

                        if (draftStatus == "in-progress")
                            importDraftNumber = draftNum;
                    }

                    db.SaveChanges();

                    // Auto-snapshot any previously in-progress draft when importing a higher draft.
                    // e.g. importing draft 2 while draft 1 is still "in-progress" → snapshot draft 1.
                    if (importDraftNumber > 0)
                    {
                        var staleInProgress = db.Drafts
                            .Where(d => d.BookProjectID == projectId
                                     && d.Status == "in-progress"
                                     && d.DraftNumber < importDraftNumber)
                            .ToList();
                        foreach (var stale in staleInProgress)
                            stale.Status = "snapshot";
                        if (staleInProgress.Any())
                            db.SaveChanges();
                    }

                    // Enforce exactly one in-progress draft per project
                    var inProgressCount = db.Drafts
                        .Count(d => d.BookProjectID == projectId && d.Status == "in-progress");

                    if (inProgressCount > 1)
                    {
                        tx.Rollback();
                        result.Success = false;
                        result.Errors.Add(string.Format(
                            "Import would leave {0} in-progress drafts for this project. " +
                            "Exactly one is allowed. Check the Status values in book.xml versioning.",
                            inProgressCount));
                        return result;
                    }

                    if (importDraftNumber == 0)
                    {
                        importDraftNumber = db.Drafts
                            .Where(d => d.BookProjectID == projectId)
                            .Select(d => d.DraftNumber)
                            .DefaultIfEmpty(1)
                            .Max();
                    }

                    result.DraftNumber = importDraftNumber;
                    result.Warnings.Add(string.Format("DIAG bookXmlPath={0} importDraftNumber={1}", bookXmlPath, importDraftNumber));

                    // Update project-level BookML fields
                    if (!string.IsNullOrEmpty(bookId))
                        project.BookmlId = bookId;
                    project.CurrentDraftNumber = importDraftNumber;

                    // ----------------------------------------------------------
                    // Process each chapter component
                    // ----------------------------------------------------------
                    var components      = ParseComponents(bookXmlPath, bookDir);
                    int totalComponents = components.Count;

                    for (int compIndex = 0; compIndex < components.Count; compIndex++)
                    {
                        var comp = components[compIndex];
                        var chapterDoc = XDocument.Load(comp.ChapterPath);
                        var metaDoc    = XDocument.Load(comp.MetaPath);
                        var notesDoc   = XDocument.Load(comp.NotesPath);

                        var chapterEl      = chapterDoc.Root;
                        var chapterBookmlId = chapterEl.Attribute("id").Value;      // e.g. "ch01"
                        var chapterDraft   = ParseInt(chapterEl.Attribute("draft")?.Value, importDraftNumber);
                        result.Warnings.Add(string.Format("DIAG file={0} draft-attr={1}",
                            comp.ChapterPath, chapterEl.Attribute("draft")?.Value ?? "(missing)"));
                        var chapterInfoEl  = chapterEl.Element(Ns + "chapterinfo");
                        var chapterTitle   = chapterInfoEl.Element(Ns + "title")?.Value ?? "";

                        int chapterNumber = 0;
                        var chNumEl = chapterInfoEl.Element(Ns + "chapternumber");
                        if (chNumEl != null)
                            int.TryParse(chNumEl.Value, out chapterNumber);
                        if (chapterNumber == 0)
                            chapterNumber = ExtractNumberFromChapterId(comp.Id);

                        // Find or create the Chapter row
                        var chapter = db.Chapters.FirstOrDefault(
                            c => c.BookProjectID == projectId && c.BookmlChapterId == chapterBookmlId);

                        if (chapter == null && chapterNumber > 0)
                            chapter = db.Chapters.FirstOrDefault(
                                c => c.BookProjectID == projectId && c.ChapterNumber == chapterNumber);

                        if (chapter == null)
                        {
                            chapter = new Chapter
                            {
                                BookProjectID = projectId,
                                ChapterNumber = chapterNumber
                            };
                            db.Chapters.Add(chapter);
                        }

                        chapter.ChapterTitle    = chapterTitle;
                        chapter.BookmlChapterId = chapterBookmlId;
                        chapter.SourceFileName  = Path.GetFileName(comp.ChapterPath);
                        chapter.SortOrder       = compIndex;
                        if (chapterNumber > 0)
                            chapter.ChapterNumber = chapterNumber;

                        db.SaveChanges(); // Ensure ChapterID is set before FK use

                        var metaByPid  = BuildMetaLookup(metaDoc);
                        var notesByPid = BuildNotesLookup(notesDoc);
                        var parsedParas = ExtractParagraphs(chapterEl);

                        // Index existing working-copy paragraphs by pid
                        var existingParas = db.Paragraphs
                            .Where(p => p.ChapterID == chapter.ChapterID)
                            .ToList()
                            .ToDictionary(p => p.UniqueID, StringComparer.OrdinalIgnoreCase);

                        var incomingPids = new HashSet<string>(
                            parsedParas.Select(p => p.Pid), StringComparer.OrdinalIgnoreCase);

                        // Pre-load ParagraphVersions for this chapter+draft in one query
                        // (avoids one SELECT per paragraph inside the loop)
                        var existingVersionPids = new HashSet<string>(
                            db.ParagraphVersions
                                .Where(v => v.ChapterID == chapter.ChapterID
                                         && v.DraftNumber == chapterDraft)
                                .Select(v => v.Pid),
                            StringComparer.OrdinalIgnoreCase);

                        // Pre-load MetaNotes and EditNotes for existing paragraphs
                        // (avoids N+1 SELECT queries and mid-loop SaveChanges calls)
                        var allParaIds = existingParas.Values.Select(p => p.ParagraphID).ToList();
                        var existingMetaByParaId = allParaIds.Any()
                            ? db.MetaNotes.Where(m => allParaIds.Contains(m.ParagraphID))
                                          .ToDictionary(m => m.ParagraphID)
                            : new Dictionary<int, MetaNote>();
                        var existingNotesByParaId = allParaIds.Any()
                            ? db.EditNotes.Where(n => allParaIds.Contains(n.ParagraphID))
                                          .ToDictionary(n => n.ParagraphID)
                            : new Dictionary<int, EditNote>();

                        int ordinal = 0;

                        // ---- Pass 1: paragraph and version records ----
                        // No SaveChanges inside the loop. New Paragraph objects are tracked
                        // by EF and receive their ParagraphID after the flush below.
                        var newParas = new List<Tuple<ParsedParagraph, Paragraph>>();

                        foreach (var pp in parsedParas)
                        {
                            ordinal++;

                            // ParagraphVersions — append-only, never overwrite
                            if (!existingVersionPids.Contains(pp.Pid))
                            {
                                db.ParagraphVersions.Add(new ParagraphVersion
                                {
                                    Pid           = pp.Pid,
                                    ChapterID     = chapter.ChapterID,
                                    DraftNumber   = chapterDraft,
                                    Seq           = pp.Seq,
                                    ParaType      = pp.ParaType,
                                    Content       = pp.Text,
                                    DraftCreated  = pp.DraftCreated,
                                    DraftModified = pp.DraftModified,
                                    ModifiedBy    = pp.ModifiedBy,
                                    ModifiedDate  = pp.ModifiedDate,
                                    ChangeType    = pp.ChangeType
                                });
                                result.VersionsRecorded++;
                            }
                            else
                            {
                                result.Warnings.Add(string.Format(
                                    "Skipped duplicate ParagraphVersion: pid={0} draft={1}",
                                    pp.Pid, chapterDraft));
                            }

                            Paragraph workingPara;
                            if (existingParas.TryGetValue(pp.Pid, out workingPara))
                            {
                                workingPara.ParagraphText    = pp.Text;
                                workingPara.OrdinalPosition  = ordinal;
                                workingPara.LastModifiedDate = DateTime.Now;
                                result.ParagraphsUpdated++;
                            }
                            else
                            {
                                workingPara = new Paragraph
                                {
                                    ChapterID        = chapter.ChapterID,
                                    UniqueID         = pp.Pid,
                                    OrdinalPosition  = ordinal,
                                    ParagraphText    = pp.Text,
                                    CreatedDate      = DateTime.Now,
                                    LastModifiedDate = DateTime.Now
                                };
                                db.Paragraphs.Add(workingPara);
                                newParas.Add(Tuple.Create(pp, workingPara));
                                result.ParagraphsAdded++;
                            }
                        }

                        // Flush paragraph inserts once so EF assigns ParagraphIDs
                        db.SaveChanges();

                        // ---- Pass 2: meta and notes ----
                        // New paragraphs: create MetaNote + EditNote with content set upfront
                        foreach (var entry in newParas)
                        {
                            var pp          = entry.Item1;
                            var workingPara = entry.Item2;
                            string metaText; metaByPid.TryGetValue(pp.Pid, out metaText);
                            string noteText; notesByPid.TryGetValue(pp.Pid, out noteText);

                            db.MetaNotes.Add(new MetaNote
                            {
                                ParagraphID = workingPara.ParagraphID,
                                UniqueID    = pp.Pid,
                                MetaText    = metaText ?? ""
                            });
                            db.EditNotes.Add(new EditNote
                            {
                                ParagraphID      = workingPara.ParagraphID,
                                UniqueID         = pp.Pid,
                                NoteText         = noteText ?? "",
                                LastModifiedDate = DateTime.Now
                            });
                        }

                        // Existing paragraphs: update MetaNote + EditNote in-memory
                        foreach (var pp in parsedParas)
                        {
                            Paragraph workingPara;
                            if (!existingParas.TryGetValue(pp.Pid, out workingPara)) continue;

                            if (metaByPid.ContainsKey(pp.Pid))
                            {
                                MetaNote meta;
                                if (existingMetaByParaId.TryGetValue(workingPara.ParagraphID, out meta))
                                    meta.MetaText = metaByPid[pp.Pid];
                            }
                            if (notesByPid.ContainsKey(pp.Pid))
                            {
                                EditNote note;
                                if (existingNotesByParaId.TryGetValue(workingPara.ParagraphID, out note)
                                    && string.IsNullOrEmpty(note.NoteText))
                                {
                                    note.NoteText         = notesByPid[pp.Pid];
                                    note.LastModifiedDate = DateTime.Now;
                                }
                            }
                        }

                        // Remove working-copy paragraphs whose pid no longer appears in the draft.
                        // Their version history is preserved in ParagraphVersions.
                        var toRemove = existingParas.Values
                            .Where(p => !incomingPids.Contains(p.UniqueID))
                            .ToList();
                        foreach (var orphan in toRemove)
                        {
                            db.Paragraphs.Remove(orphan);
                            result.ParagraphsRemoved++;
                        }

                        db.SaveChanges();
                        result.ChaptersProcessed++;
                        onChapterImported?.Invoke(result.ChaptersProcessed, totalComponents, chapterTitle,
                            result.ParagraphsAdded, result.ParagraphsUpdated);
                    }

                    project.LastModifiedDate = DateTime.Now;
                    db.SaveChanges();
                    tx.Commit();
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    result.Success = false;
                    result.Errors.Add("Import failed: " + ex.Message);
                    if (ex.InnerException != null)
                        result.Errors.Add("Detail: " + ex.InnerException.Message);
                }
            }

            return result;
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private XmlSchemaSet LoadSchemaSet()
        {
            var schemas = new XmlSchemaSet();
            var schemaFiles = new[]
            {
                "bookml-common.xsd",
                "bookml-book.xsd",
                "bookml-chapter.xsd",
                "bookml-meta.xsd",
                "bookml-notes.xsd"
            };

            foreach (var file in schemaFiles)
            {
                var fullPath = Path.GetFullPath(Path.Combine(_xsdBasePath, file));
                schemas.Add("https://bookml.org/ns/1.0", XmlReader.Create(fullPath));
            }

            schemas.Compile();
            return schemas;
        }

        private List<string> ValidateXml(string xmlPath)
        {
            var errors = new List<string>();
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas        = _schemas
            };
            settings.ValidationEventHandler += (s, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    errors.Add(string.Format("{0} line {1}: {2}",
                        Path.GetFileName(xmlPath),
                        e.Exception != null ? e.Exception.LineNumber : 0,
                        e.Message));
                }
            };

            try
            {
                using (var reader = XmlReader.Create(xmlPath, settings))
                    while (reader.Read()) { }
            }
            catch (XmlException ex)
            {
                errors.Add(string.Format("{0}: XML parse error at line {1}: {2}",
                    Path.GetFileName(xmlPath), ex.LineNumber, ex.Message));
            }

            return errors;
        }

        private List<ComponentEntry> ParseComponents(string bookXmlPath, string bookDir)
        {
            var result = new List<ComponentEntry>();
            var doc = XDocument.Load(bookXmlPath);
            var contentsEl = doc.Root.Element(Ns + "contents");
            if (contentsEl == null) return result;

            foreach (var matterName in new[] { "frontmatter", "bodymatter", "backmatter" })
            {
                var matterEl = contentsEl.Element(Ns + matterName);
                if (matterEl == null) continue;

                foreach (var compEl in matterEl.Elements(Ns + "component")
                    .OrderBy(e => ParseInt(e.Attribute("seq")?.Value, 0)))
                {
                    result.Add(new ComponentEntry
                    {
                        Id          = compEl.Attribute("id")?.Value          ?? "",
                        ChapterFile = compEl.Attribute("chapter-file")?.Value ?? "",
                        MetaFile    = compEl.Attribute("meta-file")?.Value    ?? "",
                        NotesFile   = compEl.Attribute("notes-file")?.Value   ?? "",
                        ChapterPath = ResolvePath(bookDir, compEl.Attribute("chapter-file")?.Value ?? ""),
                        MetaPath    = ResolvePath(bookDir, compEl.Attribute("meta-file")?.Value    ?? ""),
                        NotesPath   = ResolvePath(bookDir, compEl.Attribute("notes-file")?.Value   ?? "")
                    });
                }
            }

            return result;
        }

        private HashSet<string> ExtractPidsFromChapter(string chapterPath)
        {
            var pids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var doc = XDocument.Load(chapterPath);
            foreach (var el in doc.Descendants())
            {
                var pid = el.Attribute("pid")?.Value;
                if (!string.IsNullOrEmpty(pid))
                    pids.Add(pid);
            }
            return pids;
        }

        private List<string> CheckPidIntegrity(string filePath, string fileType,
            HashSet<string> chapterPids, string componentId)
        {
            var errors = new List<string>();
            var doc = XDocument.Load(filePath);
            var elementName = fileType == "meta" ? "para-meta" : "para-notes";

            foreach (var el in doc.Root.Elements(Ns + elementName))
            {
                var pid = el.Attribute("pid")?.Value;
                if (pid != null && !chapterPids.Contains(pid))
                {
                    errors.Add(string.Format(
                        "Orphan pid in {0} file for component '{1}': '{2}' has no matching paragraph. Import rejected.",
                        fileType, componentId, pid));
                }
            }
            return errors;
        }

        private List<ParsedParagraph> ExtractParagraphs(XElement chapterEl)
        {
            var result = new List<ParsedParagraph>();

            // Epigraph paragraphs sit inside <chapterinfo><epigraph> and come before section content
            var chapterInfoEl = chapterEl.Element(Ns + "chapterinfo");
            if (chapterInfoEl != null)
            {
                var epigraphEl = chapterInfoEl.Element(Ns + "epigraph");
                if (epigraphEl != null)
                {
                    foreach (var paraEl in epigraphEl.Elements(Ns + "para"))
                    {
                        var pp = ParsePara(paraEl);
                        if (pp != null) result.Add(pp);
                    }
                }
            }

            foreach (var section in chapterEl.Elements(Ns + "section")
                .OrderBy(s => ParseInt(s.Attribute("seq")?.Value, 0)))
            {
                ExtractFromSection(section, result);
            }
            return result;
        }

        private void ExtractFromSection(XElement sectionEl, List<ParsedParagraph> result)
        {
            foreach (var child in sectionEl.Elements())
            {
                if (child.Name == Ns + "section")
                    ExtractFromSection(child, result);
                else if (child.Name == Ns + "para")
                {
                    var pp = ParsePara(child);
                    if (pp != null) result.Add(pp);
                }
                // headings, figures, tables, breaks are skipped for the working-copy paragraph list
            }
        }

        private ParsedParagraph ParsePara(XElement paraEl)
        {
            var pid = paraEl.Attribute("pid")?.Value;
            if (string.IsNullOrEmpty(pid)) return null;

            return new ParsedParagraph
            {
                Pid           = pid,
                Seq           = ParseInt(paraEl.Attribute("seq")?.Value, 0),
                ParaType      = paraEl.Attribute("type")?.Value ?? "normal",
                Text          = paraEl.Value.Trim(),
                DraftCreated  = ParseInt(paraEl.Attribute("draft-created")?.Value,  1),
                DraftModified = ParseInt(paraEl.Attribute("draft-modified")?.Value, 1),
                ModifiedBy    = paraEl.Attribute("modified-by")?.Value ?? "ai",
                ModifiedDate  = ParseNullableDateTime(paraEl.Attribute("modified-date")?.Value),
                ChangeType    = paraEl.Attribute("change-type")?.Value
            };
        }

        /// <summary>
        /// Summarises the para-meta fields into a single MetaText string for the
        /// working-copy MetaNotes table. Full structured meta is preserved in the XML.
        /// </summary>
        private Dictionary<string, string> BuildMetaLookup(XDocument metaDoc)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pm in metaDoc.Root.Elements(Ns + "para-meta"))
            {
                var pid = pm.Attribute("pid")?.Value;
                if (string.IsNullOrEmpty(pid)) continue;

                var parts = new List<string>();
                Add(parts, "Status",   pm.Element(Ns + "status")?.Value);
                Add(parts, "POV",      pm.Element(Ns + "pov")?.Value);
                Add(parts, "Location", pm.Element(Ns + "location")?.Value);
                Add(parts, "Time",     pm.Element(Ns + "timepoint")?.Value);
                Add(parts, "Tone",     pm.Element(Ns + "tone")?.Value);
                Add(parts, "Topic",    pm.Element(Ns + "topic")?.Value);

                var tagsEl = pm.Element(Ns + "tags");
                if (tagsEl != null)
                {
                    var tags = tagsEl.Elements(Ns + "tag").Select(t => t.Value).ToList();
                    if (tags.Any()) parts.Add("Tags: " + string.Join(", ", tags));
                }

                if (parts.Any())
                    result[pid] = string.Join(" | ", parts);
            }

            return result;
        }

        /// <summary>
        /// Collects unresolved craft/query/continuity/research notes into a plain text
        /// string for the working-copy EditNotes table. diff-hint notes are skipped.
        /// </summary>
        private Dictionary<string, string> BuildNotesLookup(XDocument notesDoc)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pn in notesDoc.Root.Elements(Ns + "para-notes"))
            {
                var pid = pn.Attribute("pid")?.Value;
                if (string.IsNullOrEmpty(pid)) continue;

                var parts = new List<string>();
                foreach (var note in pn.Elements(Ns + "note"))
                {
                    var noteType = note.Attribute("type")?.Value ?? "general";
                    var resolved = note.Attribute("resolved")?.Value == "true";

                    if (noteType == "diff-hint") continue;  // editorial history; not for human editor
                    if (resolved) continue;

                    var body = note.Element(Ns + "body")?.Value?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(body))
                        parts.Add(string.Format("[{0}] {1}", noteType, body));
                }

                if (parts.Any())
                    result[pid] = string.Join("\n", parts);
            }

            return result;
        }

        private static void Add(List<string> parts, string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
                parts.Add(label + ": " + value);
        }

        private string ResolvePath(string baseDir, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return "";
            var normalised = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(baseDir, normalised));
        }

        private int ExtractNumberFromChapterId(string chapterId)
        {
            var m = Regex.Match(chapterId ?? "", @"\d+");
            int n;
            return m.Success && int.TryParse(m.Value, out n) ? n : 0;
        }

        private DateTime ParseDateTime(string value)
        {
            DateTime dt;
            return DateTime.TryParse(value, out dt) ? dt : DateTime.Now;
        }

        private DateTime? ParseNullableDateTime(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            DateTime dt;
            return DateTime.TryParse(value, out dt) ? dt : (DateTime?)null;
        }

        private int ParseInt(string value, int defaultValue)
        {
            int n;
            return int.TryParse(value, out n) ? n : defaultValue;
        }

        // =====================================================================
        // INNER TYPES
        // =====================================================================

        private class ComponentEntry
        {
            public string Id          { get; set; }
            public string ChapterFile { get; set; }
            public string MetaFile    { get; set; }
            public string NotesFile   { get; set; }
            public string ChapterPath { get; set; }
            public string MetaPath    { get; set; }
            public string NotesPath   { get; set; }
        }

        private class ParsedParagraph
        {
            public string    Pid           { get; set; }
            public int       Seq           { get; set; }
            public string    ParaType      { get; set; }
            public string    Text          { get; set; }
            public int       DraftCreated  { get; set; }
            public int       DraftModified { get; set; }
            public string    ModifiedBy    { get; set; }
            public DateTime? ModifiedDate  { get; set; }
            public string    ChangeType    { get; set; }
        }
    }
}
