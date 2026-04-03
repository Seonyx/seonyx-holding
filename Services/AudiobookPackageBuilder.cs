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
        public byte[] BuildPackage(SeonyxContext db, int bookProjectId, VoiceInfo voice)
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
                        BuildReadme(project.ProjectName ?? "", voice));

                    // generate_audiobook.py
                    AddTextEntry(archive, "generate_audiobook.py", PythonScript.Source);

                    // chapters/ text files
                    int chIdx = 0;
                    foreach (var ch in chapters)
                    {
                        var fileRef = chapterFiles[chIdx++];
                        string content = BuildChapterText(db, ch);
                        AddTextEntry(archive, "chapters/" + fileRef.FileName, content);
                    }
                }

                return ms.ToArray();
            }
        }

        // =====================================================================
        // Chapter text file builder
        // =====================================================================

        private string BuildChapterText(SeonyxContext db, Chapter ch)
        {
            var sb = new StringBuilder();

            // Chapter heading directive
            string title = BuildChapterTitle(ch);
            sb.AppendLine("## CHAPTER_TITLE: " + title);
            sb.AppendLine();

            // Load paragraphs and their latest version data
            var paragraphs = db.Paragraphs
                .Where(p => p.ChapterID == ch.ChapterID)
                .OrderBy(p => p.OrdinalPosition)
                .ToList();

            if (!paragraphs.Any())
                return sb.ToString();

            // Latest ParagraphVersion per pid (for ParaType and Seq) -- same pattern as EpubExporter
            var latestVersionByPid = db.ParagraphVersions
                .Where(v => v.ChapterID == ch.ChapterID)
                .ToList()
                .GroupBy(v => v.Pid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.DraftNumber).First(),
                    StringComparer.OrdinalIgnoreCase);

            // Build para rows sorted by Seq (with OrdinalPosition fallback)
            var rows = new List<ParaRow>();
            int ordinal = 0;
            foreach (var para in paragraphs)
            {
                ordinal++;
                ParagraphVersion ver = null;
                if (!string.IsNullOrEmpty(para.UniqueID))
                    latestVersionByPid.TryGetValue(para.UniqueID, out ver);

                int seq = (ver != null && ver.Seq > 0) ? ver.Seq : ordinal * 1000;

                string paraType = (ver != null && !string.IsNullOrEmpty(ver.ParaType))
                    ? ver.ParaType
                    : "normal";

                rows.Add(new ParaRow
                {
                    Seq      = seq,
                    ParaType = paraType,
                    Text     = para.ParagraphText ?? ""
                });
            }

            rows = rows.OrderBy(r => r.Seq).ToList();

            // Emit paragraphs -- all body types are read aloud as prose
            foreach (var row in rows)
            {
                if (ShouldSkip(row.ParaType))
                    continue;

                string text = NormaliseText(row.Text);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                sb.AppendLine(text);
                sb.AppendLine();
            }

            return sb.ToString();
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

        private static string BuildReadme(string bookTitle, VoiceInfo voice)
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
                voice.Quality);
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
