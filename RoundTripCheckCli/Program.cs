using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Seonyx.Web.Services;

namespace RoundTripCheckCli
{
    // Standalone round-trip verification for the BookML Inline
    // Presentation Elements parser/converter/serializer classes,
    // replacing an automated test project (none exists in this
    // solution -- see CLAUDE.md). Mirrors ContentAnalyserCli's
    // subcommand-dispatch, manual-flag-parsing, exit-code convention
    // (0 = success, 1 = failure, 2 = usage error).
    class Program
    {
        private static readonly XNamespace Ns = "https://bookml.org/ns/1.0";

        // Fixture .md files are saved as ordinary text files with a
        // trailing newline (standard editor/heredoc convention). That
        // trailing newline is not meaningful fixture content -- strip it
        // so a Markdown fixture's last fragment doesn't pick up a
        // spurious \n that expected.txt (read via ReadAllLines, which
        // strips line terminators) never had.
        private static string ReadInputMarkdown(string path)
        {
            string text = File.ReadAllText(path);
            if (text.EndsWith("\r\n")) return text.Substring(0, text.Length - 2);
            if (text.EndsWith("\n")) return text.Substring(0, text.Length - 1);
            return text;
        }

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
                case "roundtrip": return RunRoundtrip(args);
                default:
                    Console.Error.WriteLine("Unknown subcommand: " + args[0]);
                    PrintUsage();
                    return 2;
            }
        }

        // -------------------------------------------------------
        // roundtrip subcommand
        // -------------------------------------------------------
        static int RunRoundtrip(string[] args)
        {
            string fixturesDir = null;
            bool quiet = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--fixtures" && i + 1 < args.Length) { fixturesDir = args[++i]; }
                else if (args[i] == "--quiet") { quiet = true; }
            }

            if (fixturesDir == null)
            {
                Console.Error.WriteLine("roundtrip: --fixtures is required.");
                Console.Error.WriteLine("Usage: roundtrip-check roundtrip --fixtures path/to/Fixtures [--quiet]");
                return 2;
            }

            if (!Directory.Exists(fixturesDir))
            {
                Console.Error.WriteLine("Fixtures directory not found: " + fixturesDir);
                return 2;
            }

            var results = new List<FixtureResult>();

            foreach (var dir in Directory.GetDirectories(fixturesDir).OrderBy(d => d, StringComparer.Ordinal))
            {
                var manifestPath = Path.Combine(dir, "fixture.txt");
                if (!File.Exists(manifestPath))
                    continue;

                string name = Path.GetFileName(dir);
                try
                {
                    results.Add(RunFixture(dir, manifestPath));
                }
                catch (Exception ex)
                {
                    results.Add(new FixtureResult { Name = name, Pass = false, Detail = "Unhandled: " + ex.Message });
                }
            }

            if (!results.Any())
            {
                Console.Error.WriteLine("No fixtures found under " + fixturesDir + " (expected subfolders containing fixture.txt).");
                return 2;
            }

            int passCount = results.Count(r => r.Pass);

            if (!quiet)
            {
                Console.WriteLine();
                Console.WriteLine("{0,-40} {1,-6} {2}", "FIXTURE", "PASS", "DETAIL");
                Console.WriteLine(new string('-', 90));
                foreach (var r in results)
                {
                    Console.WriteLine("{0,-40} {1,-6} {2}", r.Name, r.Pass ? "PASS" : "FAIL", r.Detail ?? "");
                }
                Console.WriteLine(new string('-', 90));
                Console.WriteLine(string.Format("{0}/{1} fixtures passed.", passCount, results.Count));
            }

            return passCount == results.Count ? 0 : 1;
        }

        private class FixtureResult
        {
            public string Name;
            public bool Pass;
            public string Detail;
        }

        // fixture.txt line 1 is the kind: xml-roundtrip | markdown-parse | markdown-roundtrip | negative-nesting | negative-serialize
        private static FixtureResult RunFixture(string dir, string manifestPath)
        {
            string name = Path.GetFileName(dir);
            var lines = File.ReadAllLines(manifestPath);
            string kind = lines.Length > 0 ? lines[0].Trim() : "";

            switch (kind)
            {
                case "xml-roundtrip":
                    return RunXmlRoundtrip(dir, name);
                case "markdown-parse":
                    return RunMarkdownParse(dir, name);
                case "markdown-roundtrip":
                    return RunMarkdownRoundtrip(dir, name);
                case "negative-nesting":
                    return RunNegativeNesting(dir, name);
                case "negative-serialize":
                    return RunNegativeSerialize(dir, name);
                case "xml-coalesce":
                    return RunXmlCoalesce(dir, name);
                default:
                    return new FixtureResult { Name = name, Pass = false, Detail = "Unknown fixture kind: " + kind };
            }
        }

        // input.xml -> runs -> output.xml must equal input.xml byte-for-byte.
        private static FixtureResult RunXmlRoundtrip(string dir, string name)
        {
            string inputPath = Path.Combine(dir, "input.xml");
            string inputXml = File.ReadAllText(inputPath);
            var paraEl = XElement.Parse(inputXml);
            string pid = (string)paraEl.Attribute("pid") ?? name;

            var runs = InlineXmlRunConverter.Coalesce(InlineXmlRunConverter.FlattenToRuns(paraEl, pid));

            var rebuilt = new XElement(paraEl.Name, paraEl.Attributes());
            foreach (var node in InlineXmlRunConverter.BuildParaContent(runs))
                rebuilt.Add(node);

            string outputXml = rebuilt.ToString(SaveOptions.DisableFormatting);
            string normalizedInput = XElement.Parse(inputXml).ToString(SaveOptions.DisableFormatting);

            bool pass = string.Equals(outputXml, normalizedInput, StringComparison.Ordinal);
            return new FixtureResult
            {
                Name = name,
                Pass = pass,
                Detail = pass ? "" : "output != input\n  input:  " + normalizedInput + "\n  output: " + outputXml
            };
        }

        // input.md -> MarkdownInlineParser -> compare fragment styles against expected.txt
        // (one "TEXT|strong|emph" line per fragment). If expected.txt's
        // first line is the literal token THROWS, the parser must instead
        // throw MarkdownParseException (used for the triple-asterisk
        // rejection case) -- pass = exception thrown.
        private static FixtureResult RunMarkdownParse(string dir, string name)
        {
            string input = ReadInputMarkdown(Path.Combine(dir, "input.md"));
            string expectedPath = Path.Combine(dir, "expected.txt");
            var expectedLines = File.ReadAllLines(expectedPath);

            if (expectedLines.Length > 0 && expectedLines[0].Trim() == "THROWS")
            {
                try
                {
                    MarkdownInlineParser.Parse(input, name);
                    return new FixtureResult { Name = name, Pass = false, Detail = "expected MarkdownParseException, none thrown" };
                }
                catch (MarkdownParseException)
                {
                    return new FixtureResult { Name = name, Pass = true };
                }
            }

            var fragments = MarkdownInlineParser.Parse(input, name);

            if (fragments.Count != expectedLines.Length)
            {
                return new FixtureResult
                {
                    Name = name,
                    Pass = false,
                    Detail = string.Format("fragment count {0} != expected {1}", fragments.Count, expectedLines.Length)
                };
            }

            for (int i = 0; i < fragments.Count; i++)
            {
                var f = fragments[i];
                string actualLine = string.Format("{0}|{1}|{2}", f.Text, f.IsStrong ? "strong" : "-", f.IsEmph ? "emph" : "-");
                if (actualLine != expectedLines[i].TrimEnd('\r', '\n'))
                {
                    return new FixtureResult
                    {
                        Name = name,
                        Pass = false,
                        Detail = string.Format("fragment {0}: got [{1}], expected [{2}]", i, actualLine, expectedLines[i])
                    };
                }
            }

            return new FixtureResult { Name = name, Pass = true };
        }

        // input.md -> runs -> RunsToMarkdownSerializer -> must equal input.md byte-for-byte.
        private static FixtureResult RunMarkdownRoundtrip(string dir, string name)
        {
            string input = ReadInputMarkdown(Path.Combine(dir, "input.md"));
            var fragments = MarkdownInlineParser.Parse(input, name);
            var runs = InlineXmlRunConverter.Coalesce(fragments.Select(f => new InlineRun
            {
                Text = f.Text,
                IsEmph = f.IsEmph,
                IsStrong = f.IsStrong,
                IsSmallCaps = false,
                OverlayType = null,
                IsBreak = false
            }).ToList());

            string serialized = RunsToMarkdownSerializer.Serialize(runs, name);
            bool pass = string.Equals(serialized, input, StringComparison.Ordinal);

            return new FixtureResult
            {
                Name = name,
                Pass = pass,
                Detail = pass ? "" : string.Format("serialized [{0}] != input [{1}]", serialized, input)
            };
        }

        // input.xml contains <overlay> nested in <overlay> -- flattening must throw.
        private static FixtureResult RunNegativeNesting(string dir, string name)
        {
            string inputXml = File.ReadAllText(Path.Combine(dir, "input.xml"));
            var paraEl = XElement.Parse(inputXml);
            string pid = (string)paraEl.Attribute("pid") ?? name;

            try
            {
                InlineXmlRunConverter.FlattenToRuns(paraEl, pid);
                return new FixtureResult { Name = name, Pass = false, Detail = "expected InlineXmlNestingException, none thrown" };
            }
            catch (InlineXmlNestingException)
            {
                return new FixtureResult { Name = name, Pass = true };
            }
        }

        // input.xml contains overlay/smallcaps/linebreak -- serializing the
        // resulting runs back to Markdown must throw NonExpressibleRunException.
        private static FixtureResult RunNegativeSerialize(string dir, string name)
        {
            string inputXml = File.ReadAllText(Path.Combine(dir, "input.xml"));
            var paraEl = XElement.Parse(inputXml);
            string pid = (string)paraEl.Attribute("pid") ?? name;

            var runs = InlineXmlRunConverter.Coalesce(InlineXmlRunConverter.FlattenToRuns(paraEl, pid));

            try
            {
                RunsToMarkdownSerializer.Serialize(runs, pid);
                return new FixtureResult { Name = name, Pass = false, Detail = "expected NonExpressibleRunException, none thrown" };
            }
            catch (NonExpressibleRunException)
            {
                return new FixtureResult { Name = name, Pass = true };
            }
        }

        // input.xml has two or more adjacent XML elements with identical
        // style (e.g. two sibling <emph> elements with no unstyled text
        // between them) -- expected.xml is the coalesced canonical form:
        // flatten (which naturally produces adjacent same-style runs from
        // adjacent same-style elements) -> Coalesce -> rebuild must equal
        // expected.xml, and must NOT equal the (un-coalesced) input.xml.
        private static FixtureResult RunXmlCoalesce(string dir, string name)
        {
            string inputXml = File.ReadAllText(Path.Combine(dir, "input.xml"));
            string expectedXml = File.ReadAllText(Path.Combine(dir, "expected.xml"));
            var paraEl = XElement.Parse(inputXml);
            string pid = (string)paraEl.Attribute("pid") ?? name;

            var runs = InlineXmlRunConverter.Coalesce(InlineXmlRunConverter.FlattenToRuns(paraEl, pid));

            var rebuilt = new XElement(paraEl.Name, paraEl.Attributes());
            foreach (var node in InlineXmlRunConverter.BuildParaContent(runs))
                rebuilt.Add(node);

            string outputXml = rebuilt.ToString(SaveOptions.DisableFormatting);
            string normalizedExpected = XElement.Parse(expectedXml).ToString(SaveOptions.DisableFormatting);

            bool pass = string.Equals(outputXml, normalizedExpected, StringComparison.Ordinal);
            return new FixtureResult
            {
                Name = name,
                Pass = pass,
                Detail = pass ? "" : "coalesced output != expected\n  expected: " + normalizedExpected + "\n  output:   " + outputXml
            };
        }

        private static void PrintUsage()
        {
            Console.WriteLine("roundtrip-check -- BookML inline presentation round-trip verification");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  roundtrip-check roundtrip --fixtures path/to/Fixtures [--quiet]");
            Console.WriteLine();
            Console.WriteLine("Fixture kinds (first line of each fixture's fixture.txt):");
            Console.WriteLine("  xml-roundtrip       input.xml -> runs -> output.xml, must equal input.xml byte-for-byte");
            Console.WriteLine("  markdown-parse       input.md -> fragments, compared against expected.txt (one 'TEXT|strong|emph' line per fragment)");
            Console.WriteLine("  markdown-roundtrip   input.md -> runs -> serialized Markdown, must equal input.md byte-for-byte");
            Console.WriteLine("  negative-nesting     input.xml has <overlay> nested in <overlay>, flattening must throw InlineXmlNestingException");
            Console.WriteLine("  negative-serialize   input.xml has overlay/smallcaps/linebreak, serializing to Markdown must throw NonExpressibleRunException");
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0 = all fixtures passed, 1 = one or more failed, 2 = usage error.");
        }
    }
}
