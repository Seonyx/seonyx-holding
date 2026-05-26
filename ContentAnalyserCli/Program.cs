using System;
using System.IO;
using ContentAnalysisEngine;

namespace ContentAnalyserCli
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Length == 0)
            {
                PrintUsage();
                return 2;
            }

            string subcommand = args[0].ToLowerInvariant();

            switch (subcommand)
            {
                case "analyse":       return RunAnalyse(args);
                case "workorder":     return RunWorkorder(args);
                case "validate":      return RunValidate(args);
                case "analyse-book":  return RunAnalyseBook(args);
                case "compare-books": return RunCompareBooks(args);
                default:
                    Console.Error.WriteLine("Unknown subcommand: " + args[0]);
                    PrintUsage();
                    return 2;
            }
        }

        // -------------------------------------------------------
        // analyse subcommand
        // -------------------------------------------------------
        static int RunAnalyse(string[] args)
        {
            string chapterPath = null;
            string namesPath   = null;
            bool   quiet       = false;

            for (int i = 1; i < args.Length; i++)
            {
                if      (args[i] == "--chapter" && i + 1 < args.Length) { chapterPath = args[++i]; }
                else if (args[i] == "--names"   && i + 1 < args.Length) { namesPath   = args[++i]; }
                else if (args[i] == "--quiet") { quiet = true; }
            }

            if (chapterPath == null)
            {
                Console.Error.WriteLine("analyse: --chapter is required.");
                Console.Error.WriteLine("Usage: content-analyser analyse --chapter path/to/chapter.xml [--names path/to/names.xml] [--quiet]");
                return 2;
            }

            try
            {
                var report = new ChapterAnalyser().Analyse(chapterPath, namesPath);
                if (!quiet)
                    Console.WriteLine(report.ToXDocument().ToString());
                return 0;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine("File not found: " + ex.FileName);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        // -------------------------------------------------------
        // workorder subcommand
        // -------------------------------------------------------
        static int RunWorkorder(string[] args)
        {
            string chapterPath = null;
            string outputPath  = null;
            string namesPath   = null;
            int    draft       = 1;
            bool   quiet       = false;

            for (int i = 1; i < args.Length; i++)
            {
                if      (args[i] == "--chapter" && i + 1 < args.Length) { chapterPath = args[++i]; }
                else if (args[i] == "--output"  && i + 1 < args.Length) { outputPath  = args[++i]; }
                else if (args[i] == "--names"   && i + 1 < args.Length) { namesPath   = args[++i]; }
                else if (args[i] == "--draft"   && i + 1 < args.Length)
                {
                    int n;
                    if (int.TryParse(args[++i], out n)) draft = n;
                }
                else if (args[i] == "--quiet") { quiet = true; }
            }

            if (chapterPath == null || outputPath == null)
            {
                Console.Error.WriteLine("workorder: --chapter, --output, and --names are required.");
                Console.Error.WriteLine("Usage: content-analyser workorder --chapter path/to/chapter.xml --output path/to/workorders.xml --names path/to/names.xml [--draft N] [--quiet]");
                return 2;
            }

            if (namesPath == null)
            {
                Console.Error.WriteLine("workorder: --names is required.");
                Console.Error.WriteLine("Cannot generate work orders without a character names file. Populate the character name table in the editor and re-export.");
                return 2;
            }

            if (!File.Exists(namesPath))
            {
                Console.Error.WriteLine("Names file not found: " + namesPath);
                return 2;
            }

            var names = ContentAnalysisEngine.NamesReader.ReadNames(namesPath);
            if (names.Count == 0)
            {
                Console.Error.WriteLine("Names file is empty -- populate the character name table in the editor and re-export.");
                return 2;
            }

            try
            {
                var report   = new ChapterAnalyser().Analyse(chapterPath, namesPath);
                if (!quiet)
                    Console.WriteLine(report.ToXDocument().ToString());

                var manifest = new WorkOrderGenerator().Generate(report, report.ChapterId, draft);
                manifest.Save(outputPath);
                return 0;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine("File not found: " + ex.FileName);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        // -------------------------------------------------------
        // validate subcommand
        // -------------------------------------------------------
        static int RunValidate(string[] args)
        {
            string originalPath  = null;
            string rewrittenPath = null;
            string entryId       = null;
            string manifestPath  = null;
            string namesPath     = null;
            string baselinesDir  = null;

            for (int i = 1; i < args.Length; i++)
            {
                if      (args[i] == "--original"   && i + 1 < args.Length) { originalPath  = args[++i]; }
                else if (args[i] == "--rewritten"   && i + 1 < args.Length) { rewrittenPath = args[++i]; }
                else if (args[i] == "--entry-id"    && i + 1 < args.Length) { entryId       = args[++i]; }
                else if (args[i] == "--manifest"    && i + 1 < args.Length) { manifestPath  = args[++i]; }
                else if (args[i] == "--names"       && i + 1 < args.Length) { namesPath     = args[++i]; }
                else if (args[i] == "--baselines"   && i + 1 < args.Length) { baselinesDir  = args[++i]; }
            }

            if (originalPath == null || rewrittenPath == null || entryId == null || manifestPath == null)
            {
                Console.Error.WriteLine("validate: --original, --rewritten, --entry-id, --manifest, and --names are all required.");
                Console.Error.WriteLine("Usage: content-analyser validate --original path/to/original.xml --rewritten path/to/rewritten.xml --entry-id WO-001 --manifest path/to/manifest.xml --names path/to/names.xml");
                return 2;
            }

            if (namesPath == null)
            {
                Console.Error.WriteLine("validate: --names is required.");
                Console.Error.WriteLine("Cannot validate rewrites without a character names file. Populate the character name table in the editor and re-export.");
                return 2;
            }

            if (!File.Exists(originalPath))
            {
                Console.Error.WriteLine("File not found: " + originalPath);
                return 2;
            }
            if (!File.Exists(rewrittenPath))
            {
                Console.Error.WriteLine("File not found: " + rewrittenPath);
                return 2;
            }
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine("File not found: " + manifestPath);
                return 2;
            }
            if (!File.Exists(namesPath))
            {
                Console.Error.WriteLine("File not found: " + namesPath);
                return 2;
            }

            var names = ContentAnalysisEngine.NamesReader.ReadNames(namesPath);
            if (names.Count == 0)
            {
                Console.Error.WriteLine("Names file is empty -- populate the character name table in the editor and re-export.");
                return 2;
            }

            try
            {
                var baselines = baselinesDir != null
                    ? WorkOrderValidator.LoadBaselines(baselinesDir, entryId)
                    : null;

                var validator = new WorkOrderValidator();
                var result    = validator.Validate(originalPath, rewrittenPath, manifestPath, entryId, namesPath, baselines);

                if (result.Passed)
                {
                    Console.WriteLine("PASS");
                    return 0;
                }
                else
                {
                    foreach (var diagnostic in result.Diagnostics)
                        Console.Error.WriteLine(diagnostic);
                    Console.WriteLine("FAIL");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        // -------------------------------------------------------
        // analyse-book subcommand
        // -------------------------------------------------------
        static int RunAnalyseBook(string[] args)
        {
            string manifestPath = null;
            string namesPath    = null;
            string outputPath   = null;
            string summaryPath  = null;

            for (int i = 1; i < args.Length; i++)
            {
                if      (args[i] == "--manifest" && i + 1 < args.Length) { manifestPath = args[++i]; }
                else if (args[i] == "--names"    && i + 1 < args.Length) { namesPath    = args[++i]; }
                else if (args[i] == "--output"   && i + 1 < args.Length) { outputPath   = args[++i]; }
                else if (args[i] == "--summary"  && i + 1 < args.Length) { summaryPath  = args[++i]; }
            }

            if (manifestPath == null || outputPath == null)
            {
                Console.Error.WriteLine("analyse-book: --manifest and --output are required.");
                Console.Error.WriteLine("Usage: content-analyser analyse-book --manifest path/to/book.xml --names path/to/names.xml --output path/to/report.xml [--summary path/to/summary.txt]");
                return 2;
            }

            if (namesPath == null)
            {
                Console.Error.WriteLine("analyse-book: --names is required.");
                Console.Error.WriteLine("Cannot run book-level analysis without a character names file. Populate the character table in the editor and re-export.");
                return 2;
            }

            if (!File.Exists(namesPath))
            {
                Console.Error.WriteLine("Names file not found: " + namesPath);
                return 2;
            }

            var names = ContentAnalysisEngine.NamesReader.ReadNames(namesPath);
            if (names.Count == 0)
            {
                Console.Error.WriteLine("Names file is empty -- populate the character table in the editor and re-export.");
                return 2;
            }

            try
            {
                var analyser = new BookAnalyser();
                var report   = analyser.Analyse(manifestPath, namesPath);

                var doc = report.ToXDocument();
                doc.Save(outputPath);
                Console.WriteLine("Book analysis saved to: " + outputPath);

                var summary = LexicalSummaryGenerator.Generate(report);
                if (summaryPath != null)
                {
                    File.WriteAllText(summaryPath, summary, System.Text.Encoding.UTF8);
                    Console.WriteLine("Lexical summary saved to: " + summaryPath);
                }
                else
                {
                    Console.WriteLine();
                    Console.Write(summary);
                }

                return 0;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine("File not found: " + ex.FileName);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        // -------------------------------------------------------
        // compare-books subcommand
        // -------------------------------------------------------
        static int RunCompareBooks(string[] args)
        {
            string fromPath    = null;
            string toPath      = null;
            string outputPath  = null;
            string summaryPath = null;
            int    fromDraft   = 0;
            int    toDraft     = 0;

            for (int i = 1; i < args.Length; i++)
            {
                if      (args[i] == "--from"       && i + 1 < args.Length) { fromPath    = args[++i]; }
                else if (args[i] == "--to"         && i + 1 < args.Length) { toPath      = args[++i]; }
                else if (args[i] == "--output"     && i + 1 < args.Length) { outputPath  = args[++i]; }
                else if (args[i] == "--summary"    && i + 1 < args.Length) { summaryPath = args[++i]; }
                else if (args[i] == "--from-draft" && i + 1 < args.Length) { int n; if (int.TryParse(args[++i], out n)) fromDraft = n; }
                else if (args[i] == "--to-draft"   && i + 1 < args.Length) { int n; if (int.TryParse(args[++i], out n)) toDraft   = n; }
            }

            if (fromPath == null || toPath == null || outputPath == null || fromDraft == 0 || toDraft == 0)
            {
                Console.Error.WriteLine("compare-books: --from, --to, --output, --from-draft, and --to-draft are all required.");
                Console.Error.WriteLine("Usage: content-analyser compare-books --from path/to/draft-N.xml --to path/to/draft-N+1.xml --from-draft N --to-draft N+1 --output path/to/delta.xml [--summary path/to/summary.txt]");
                return 2;
            }

            if (!File.Exists(fromPath))
            {
                Console.Error.WriteLine("File not found: " + fromPath);
                return 2;
            }
            if (!File.Exists(toPath))
            {
                Console.Error.WriteLine("File not found: " + toPath);
                return 2;
            }

            try
            {
                var fromDoc    = System.Xml.Linq.XDocument.Load(fromPath);
                var toDoc      = System.Xml.Linq.XDocument.Load(toPath);
                var fromReport = BookAnalysisReport.FromXDocument(fromDoc);
                var toReport   = BookAnalysisReport.FromXDocument(toDoc);

                var delta = BookDeltaComparer.Compare(fromReport, toReport, fromDraft, toDraft);

                delta.ToXDocument().Save(outputPath);
                Console.WriteLine("Delta report saved to: " + outputPath);

                string summaryText = delta.ToSummaryText();
                if (summaryPath != null)
                {
                    File.WriteAllText(summaryPath, summaryText, System.Text.Encoding.UTF8);
                    Console.WriteLine("Delta summary saved to: " + summaryPath);
                }
                else
                {
                    Console.WriteLine();
                    Console.Write(summaryText);
                }

                return 0;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine("File not found: " + ex.FileName);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        // -------------------------------------------------------
        // Usage
        // -------------------------------------------------------
        static void PrintUsage()
        {
            Console.Error.WriteLine("content-analyser <subcommand> [options]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Subcommands:");
            Console.Error.WriteLine("  analyse      --chapter <path> [--names <path>] [--quiet]");
            Console.Error.WriteLine("  workorder    --chapter <path> --output <path> --names <path> [--draft N] [--quiet]");
            Console.Error.WriteLine("  validate     --original <path> --rewritten <path> --entry-id <id> --manifest <path> --names <path> [--baselines <dir>]");
            Console.Error.WriteLine("  analyse-book --manifest <book.xml> --names <path> --output <report.xml> [--summary <summary.txt>]");
            Console.Error.WriteLine("  compare-books --from <draft-N.xml> --to <draft-N+1.xml> --from-draft N --to-draft N+1 --output <delta.xml> [--summary <summary.txt>]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Notes:");
            Console.Error.WriteLine("  --names is optional for analyse (names appear as outliers when omitted).");
            Console.Error.WriteLine("  --names is required for workorder, validate, and analyse-book.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Exit codes: 0=success, 1=failure/error, 2=usage error");
        }
    }
}
