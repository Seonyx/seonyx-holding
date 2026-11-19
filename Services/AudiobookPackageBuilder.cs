using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Seonyx.Web.Models;

namespace Seonyx.Web.Services
{
    public class AudiobookPackageBuilder
    {
        // =====================================================================
        // Public entry point
        // =====================================================================

        /// <summary>
        /// Builds the audiobook package ZIP for the given project and voice.
        /// Returns the ZIP bytes ready to stream to the browser.
        /// </summary>
        public byte[] BuildPackage(SeonyxContext db, int bookProjectId, VoiceInfo voice, int draftNumber,
            bool includeOverlays = false)
        {
            if (voice == null)
                throw new ArgumentNullException("voice");

            var project = db.BookProjects.Find(bookProjectId);
            if (project == null)
                throw new ArgumentException("Project not found: " + bookProjectId);

            var chapters = db.Chapters
                .Where(c => c.BookProjectID == bookProjectId)
                .OrderBy(c => c.SortOrder)
                .ToList();

            // Build chapter file metadata first (needed by config.json)
            var chapterFiles = new List<ChapterFileRef>();
            int idx = 1;
            foreach (var ch in chapters)
            {
                string title    = BuildChapterTitle(ch);
                string fileName = string.Format("{0:D2}-{1}.txt", idx, Slugify(title));
                chapterFiles.Add(new ChapterFileRef
                {
                    Index    = idx,
                    FileName = fileName,
                    Title    = title
                });
                idx++;
            }

            // Assemble ZIP in memory
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // config.json
                    var config = new
                    {
                        bookTitle    = project.ProjectName ?? "",
                        author       = project.Author ?? "",
                        voiceId      = voice.VoiceId,
                        speakerId    = voice.SpeakerId,
                        voiceLabel   = voice.DisplayName,
                        accent       = voice.AccentLabel,
                        gender       = voice.Gender,
                        quality      = voice.Quality,
                        chapterFiles = chapterFiles
                    };
                    AddTextEntry(archive, "config.json",
                        JsonConvert.SerializeObject(config, Formatting.Indented));

                    // README.txt
                    AddTextEntry(archive, "README.txt",
                        BuildReadme(project.ProjectName ?? "", voice, includeOverlays));

                    // generate_audiobook.py
                    AddTextEntry(archive, "generate_audiobook.py", PythonScript.Source);

                    // chapters/ text files
                    int chIdx = 0;
                    foreach (var ch in chapters)
                    {
                        var fileRef = chapterFiles[chIdx++];
                        string content = BuildChapterText(db, ch, draftNumber, includeOverlays);
                        AddTextEntry(archive, "chapters/" + fileRef.FileName, content);
                    }
                }

                return ms.ToArray();
            }
        }

        // =====================================================================
        // Chapter text file builder
        // =====================================================================

        private string BuildChapterText(SeonyxContext db, Chapter ch, int draftNumber, bool includeOverlays)
        {
            var sb = new StringBuilder();

            // Chapter heading directive
            string title = BuildChapterTitle(ch);
            sb.AppendLine("## CHAPTER_TITLE: " + title);
            sb.AppendLine();

            // Working-copy paragraphs are the source of truth for text and
            // presence (one row per pid, enforced by UQ_UniqueID_Per_Chapter).
            // ParagraphVersions is append-only history and may contain stale/
            // orphaned pids left behind by past imports -- never source
            // paragraph text or the paragraph list itself from it directly.
            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == ch.ChapterID)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            if (!paragraphs.Any())
                return sb.ToString();

            // Latest ParagraphVersion per pid, used only for Seq/ParaType metadata.
            var versionsByPid = db.ParagraphVersions
                .Where(v => v.ChapterID == ch.ChapterID && v.DraftNumber <= draftNumber)
                .ToList()
                .GroupBy(v => v.Pid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.DraftNumber).First(),
                    StringComparer.OrdinalIgnoreCase);

            // Formatting runs; a paragraph with text but none uses the shared
            // reader fallback (PlainTextRunBuilder.EffectiveRuns).
            var paraIds = paragraphs.Select(p => p.ParagraphID).ToList();
            var runsByParaId = db.ParagraphRuns
                .Where(r => paraIds.Contains(r.ParagraphID))
                .ToList()
                .GroupBy(r => r.ParagraphID)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Seq).ToList());

            // Build para rows from the working copy, sorted by Seq
            var rows = new List<ParaRow>();
            int fallback = 0;
            foreach (var para in paragraphs)
            {
                fallback++;
                ParagraphVersion ver;
                versionsByPid.TryGetValue(para.UniqueID ?? "", out ver);

                List<ParagraphRun> stored;
                runsByParaId.TryGetValue(para.ParagraphID, out stored);
                string runsWarning;
                var runs = PlainTextRunBuilder.EffectiveRuns(para.ParagraphText, para.UniqueID,
                    ParagraphRunService.ToInlineRuns(stored ?? new List<ParagraphRun>()), out runsWarning);

                rows.Add(new ParaRow
                {
                    Seq      = (ver != null && ver.Seq > 0) ? ver.Seq : fallback * 1000,
                    ParaType = (ver != null && !string.IsNullOrEmpty(ver.ParaType)) ? ver.ParaType : "normal",
                    Text     = para.ParagraphText ?? "",
                    Runs     = runs
                });
            }

            rows = rows.OrderBy(r => r.Seq).ToList();

            // Emit paragraphs -- all body types are read aloud as prose
            foreach (var row in rows)
            {
                // Any structural break becomes the script's scene-break pause.
                if (BreakKinds.IsBreak(row.ParaType))
                {
                    sb.AppendLine("## SCENE_BREAK");
                    sb.AppendLine();
                    continue;
                }

                if (ShouldSkip(row.ParaType))
                    continue;

                var lines = ParagraphLines(row.Runs, includeOverlays);
                if (lines.Count == 0)
                    continue;

                foreach (var line in lines)
                    sb.AppendLine(line);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // =====================================================================
        // Paragraph text from runs (BookML Amendment 2, section 3)
        // =====================================================================

        // The lines of one paragraph in the chapter text file. Formatting is
        // dropped (Piper has no emphasis prosody); a line break starts a new
        // line. Overlay text is skipped unless includeOverlays, in which case
        // it gets its own line between two "## PAUSE" directives. The overlay
        // TYPE is never consulted, so output cannot depend on it.
        private static List<string> ParagraphLines(IList<InlineRun> runs, bool includeOverlays)
        {
            var lines = new List<string>();
            var current = new StringBuilder();

            Action flush = () =>
            {
                var t = NormaliseText(current.ToString());
                if (!string.IsNullOrEmpty(t)) lines.Add(t);
                current.Clear();
            };

            int i = 0;
            while (i < runs.Count)
            {
                var r = runs[i];
                if (r.IsBreak) { flush(); i++; continue; }
                if (r.OverlayType == null) { current.Append(r.Text); i++; continue; }

                var overlay = new StringBuilder();
                while (i < runs.Count && !runs[i].IsBreak && runs[i].OverlayType != null)
                {
                    overlay.Append(runs[i].Text);
                    i++;
                }

                if (!includeOverlays)
                {
                    // Keep a word boundary where the overlay was cut out.
                    current.Append(' ');
                    continue;
                }

                flush();
                var spoken = NormaliseText(overlay.ToString());
                if (!string.IsNullOrEmpty(spoken))
                {
                    lines.Add("## PAUSE");
                    lines.Add(spoken);
                    lines.Add("## PAUSE");
                }
            }
            flush();
            return lines;
        }

        // =====================================================================
        // Chapter title helper
        // =====================================================================

        private static string BuildChapterTitle(Chapter ch)
        {
            string number = ch.ChapterNumber > 0 ? ch.ChapterNumber.ToString() : "";
            string title  = (ch.ChapterTitle ?? "").Trim();

            if (!string.IsNullOrEmpty(number) && !string.IsNullOrEmpty(title))
                return number + " - " + title;
            if (!string.IsNullOrEmpty(title))
                return title;
            if (!string.IsNullOrEmpty(number))
                return number;
            return "Chapter";
        }

        // =====================================================================
        // ParaType filter -- skip types that don't belong in audio
        // =====================================================================

        private static readonly HashSet<string> SkippedTypes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            // Epigraphs are skipped from body audio; they appear on their own
            // EPUB page but have no clean equivalent in a linear audio stream.
            "epigraph"
        };

        private static bool ShouldSkip(string paraType)
        {
            return SkippedTypes.Contains(paraType ?? "");
        }

        // =====================================================================
        // Text normalisation
        // =====================================================================

        private static readonly Regex DoubleSpaces = new Regex(@"  +");

        private static string NormaliseText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Em dash -> spaced hyphen (TTS-friendly pause)
            text = text.Replace("\u2014", " - ");

            // En dash -> spaced hyphen
            text = text.Replace("\u2013", " - ");

            // Curly quotes -> straight quotes (some TTS engines handle better)
            text = text.Replace("\u2018", "'").Replace("\u2019", "'");
            text = text.Replace("\u201C", "\"").Replace("\u201D", "\"");

            // Collapse double spaces
            text = DoubleSpaces.Replace(text, " ");

            return text.Trim();
        }

        // =====================================================================
        // Slug helper
        // =====================================================================

        private static readonly Regex NonAlphanumeric = new Regex(@"[^a-z0-9]+");

        private static string Slugify(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "chapter";
            string lower = title.ToLowerInvariant();
            string slug  = NonAlphanumeric.Replace(lower, "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "chapter" : slug;
        }

        // =====================================================================
        // README.txt content
        // =====================================================================

        private static string BuildReadme(string bookTitle, VoiceInfo voice, bool includeOverlays)
        {
            return string.Format(
@"======================================================
  Audiobook Package - {0}
======================================================

WHAT'S IN THIS PACKAGE
-----------------------
  generate_audiobook.py   Script that produces the audio files
  config.json             Voice and chapter settings (pre-configured)
  chapters/               Chapter text files (one per chapter)
  output/                 Audio files will appear here after you run the script

HOW TO RUN
----------
1. Extract this ZIP to a folder on your computer.

2. Open a terminal / command prompt in that folder:
   - Windows 10: Shift + right-click the folder -> ""Open PowerShell window here""
   - Linux Mint: Right-click the folder -> ""Open Terminal Here""

3. Run:
       python generate_audiobook.py

   (On Linux you may need:  python3 generate_audiobook.py)

4. The first run will download Piper TTS (~15 MB) and the voice model
   (~30-60 MB). These are saved in a .piper_cache folder inside the
   package and will not be re-downloaded on subsequent runs.

5. Audio files will appear in the output/ folder when complete.
   WAV files are always produced. MP3 files are also produced if
   ffmpeg is installed on your system.

VOICE
-----
  {1} ({2}, {3}) - {4} quality

ESTIMATED TIME
--------------
  Roughly 1-2 minutes of processing per hour of audio on a modern laptop.
  A full novel (~8 hours of audio) takes approximately 10-15 minutes.

OVERLAYS
--------
  {5}

TROUBLESHOOTING
---------------
  ""python not found""
      -> Make sure Python 3.8+ is installed and on your PATH.
        Windows: https://www.python.org/downloads/
        Linux Mint: sudo apt install python3

  ""piper failed"" error
      -> Delete the .piper_cache folder and run the script again
        to re-download a fresh copy.

  WAV files produced but no MP3
      -> Install ffmpeg: https://ffmpeg.org/download.html
        Windows: Add ffmpeg/bin to your system PATH.
        Linux Mint: sudo apt install ffmpeg
======================================================
",
                bookTitle,
                voice.DisplayName,
                voice.AccentLabel,
                voice.Gender,
                voice.Quality,
                includeOverlays
                    ? "Overlay text (on-screen text such as HUD banners and sponsored alerts)" + Environment.NewLine +
                      "  is INCLUDED: it is read aloud with a short pause before and after it."
                    : "Overlay text (on-screen text such as HUD banners and sponsored alerts)" + Environment.NewLine +
                      "  is NOT read aloud. To include it, tick \"Include overlays\" when exporting.");
        }

        // =====================================================================
        // ZIP helpers
        // =====================================================================

        private static void AddTextEntry(ZipArchive archive, string path, string text)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(text);
            }
        }

        // =====================================================================
        // Inner types
        // =====================================================================

        private class ParaRow
        {
            public int    Seq      { get; set; }
            public string ParaType { get; set; }
            public string Text     { get; set; }
            public IList<InlineRun> Runs { get; set; } = new List<InlineRun>();
        }

        // Must be serialisable by Newtonsoft — use public properties
        private class ChapterFileRef
        {
            [JsonProperty("index")]    public int    Index    { get; set; }
            [JsonProperty("fileName")] public string FileName { get; set; }
            [JsonProperty("title")]    public string Title    { get; set; }
        }
    }
}
