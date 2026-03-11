using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Seonyx.Web.Services
{
    public class BookFileParser
    {
        // Regex for [[ID]] bracket format (spec format)
        private static readonly Regex BracketPattern = new Regex(
            @"\[\[([A-Za-z0-9\-]+)\]\]\s*(.*?)(?=\[\[|$)",
            RegexOptions.Singleline);

        // Regex for pipe-delimited format: ID|text (one per line)
        private static readonly Regex PipePattern = new Regex(
            @"^([A-Za-z0-9]{6,})\|(.+)$",
            RegexOptions.Multiline);

        private static readonly Regex ChapterNumberFromFilename = new Regex(
            @"[Cc]h(\d{2})_",
            RegexOptions.IgnoreCase);

        private static readonly Regex ChapterHeading = new Regex(
            @"^#\s+Chapter\s+\d+\s*[-\u2013\u2014]\s*(.+)$",
            RegexOptions.Multiline);

        public ParsedChapter ParseChapterFile(string content, string fileName)
        {
            var result = new ParsedChapter();
            result.SourceFileName = fileName;

            // Extract chapter number from filename
            var chNumMatch = ChapterNumberFromFilename.Match(fileName);
            if (chNumMatch.Success)
            {
                result.ChapterNumber = int.Parse(chNumMatch.Groups[1].Value);
            }

            // Extract chapter title from heading, or fall back to filename
            var titleMatch = ChapterHeading.Match(content);
            if (titleMatch.Success)
            {
                result.ChapterTitle = titleMatch.Groups[1].Value.Trim();
            }
            else
            {
                result.ChapterTitle = ExtractTitleFromFilename(fileName);
            }

            // Extract POV, Setting, Chapter purpose (only in bracket/markdown format)
            result.POV = ExtractSection(content, "## POV");
            result.Setting = ExtractSection(content, "## Setting");
            result.ChapterPurpose = ExtractSection(content, "## Chapter purpose");

            // Try bracket format first: [[ID]] text
            var bracketMatches = BracketPattern.Matches(content);
            if (bracketMatches.Count > 0)
            {
                int ordinal = 1;
                foreach (Match match in bracketMatches)
                {
                    result.Paragraphs.Add(new ParsedParagraph
                    {
                        UniqueID = match.Groups[1].Value.Trim(),
                        Text = match.Groups[2].Value.Trim(),
                        OrdinalPosition = ordinal++
                    });
                }
            }
            else
            {
                // Fall back to pipe-delimited format: ID|text
                var pipeMatches = PipePattern.Matches(content);
                int ordinal = 1;
                foreach (Match match in pipeMatches)
                {
                    result.Paragraphs.Add(new ParsedParagraph
                    {
                        UniqueID = match.Groups[1].Value.Trim(),
                        Text = match.Groups[2].Value.Trim(),
                        OrdinalPosition = ordinal++
                    });
                }
            }

            return result;
        }

        public Dictionary<string, string> ParseMetaFile(string content)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Try bracket format first: [[ID]] text
            var bracketMatches = BracketPattern.Matches(content);
            if (bracketMatches.Count > 0)
            {
                foreach (Match match in bracketMatches)
                {
                    var uniqueId = match.Groups[1].Value.Trim();
                    var metaText = match.Groups[2].Value.Trim();
                    if (!result.ContainsKey(uniqueId))
                        result[uniqueId] = metaText;
                }
                return result;
            }

            // Fall back to pipe-delimited: ID|text or ID|ordinal|text
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split(new[] { '|' }, 3);
                if (parts.Length < 2) continue;

                var uniqueId = parts[0].Trim();
                if (!Regex.IsMatch(uniqueId, @"^[A-Za-z0-9\-]+$")) continue;

                string metaText;
                if (parts.Length == 3)
                {
                    // ID|ordinal|text — skip the ordinal
                    metaText = parts[2].Trim();
                }
                else
                {
                    metaText = parts[1].Trim();
                }

                if (!result.ContainsKey(uniqueId))
                    result[uniqueId] = metaText;
            }

            return result;
        }

        public Dictionary<string, string> ParseNotesFile(string content)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var pipeIndex = trimmed.IndexOf('|');
                if (pipeIndex <= 0) continue;

                var uniqueId = trimmed.Substring(0, pipeIndex).Trim();
                if (!Regex.IsMatch(uniqueId, @"^[A-Za-z0-9\-]+$")) continue;

                var noteText = trimmed.Substring(pipeIndex + 1).Trim();

                if (!result.ContainsKey(uniqueId))
                    result[uniqueId] = noteText;
            }

            return result;
        }

        public string GenerateUniqueID(int chapterNumber)
        {
            string prefix = string.Format("C{0:D2}-", chapterNumber);
            string random = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            return prefix + random;
        }

        public string ClassifyFile(string fileName)
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("_meta"))
                return "Meta";
            if (lower.Contains("_notes"))
                return "Notes";
            if (ChapterNumberFromFilename.IsMatch(fileName))
                return "Chapter";
            return "Unknown";
        }

        public int? ExtractChapterNumberFromFilename(string fileName)
        {
            var match = ChapterNumberFromFilename.Match(fileName);
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
            return null;
        }

        private string ExtractTitleFromFilename(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var match = ChapterNumberFromFilename.Match(name);
            if (match.Success)
            {
                name = name.Substring(match.Index + match.Length);
            }
            name = Regex.Replace(name, @"_(meta|notes)$", "", RegexOptions.IgnoreCase);
            name = name.Replace('_', ' ').Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private string ExtractSection(string content, string heading)
        {
            var pattern = new Regex(
                Regex.Escape(heading) + @"\s*\n(.*?)(?=\n##|\n#\s|$)",
                RegexOptions.Singleline);
            var match = pattern.Match(content);
            if (match.Success)
                return match.Groups[1].Value.Trim();
            return null;
        }
    }

    public class ParsedChapter
    {
        public int ChapterNumber { get; set; }
        public string ChapterTitle { get; set; }
        public string POV { get; set; }
        public string Setting { get; set; }
        public string ChapterPurpose { get; set; }
        public string SourceFileName { get; set; }
        public List<ParsedParagraph> Paragraphs { get; set; }

        public ParsedChapter()
        {
            Paragraphs = new List<ParsedParagraph>();
        }
    }

    public class ParsedParagraph
    {
        public string UniqueID { get; set; }
        public string Text { get; set; }
        public int OrdinalPosition { get; set; }
    }
}
