using System;
using System.IO;
using ContentAnalysisEngine;

namespace ContentAnalysisHarness
{
    class Program
    {
        static int Main(string[] args)
        {
            string chapterPath   = null;
            string workorderPath = null;
            int    draftNumber   = 1;
            bool   quiet         = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--chapter"   && i + 1 < args.Length) { chapterPath   = args[i + 1]; i++; }
                else if (args[i] == "--workorder" && i + 1 < args.Length) { workorderPath = args[i + 1]; i++; }
                else if (args[i] == "--draft"     && i + 1 < args.Length)
                {
                    int n;
                    if (int.TryParse(args[i + 1], out n)) draftNumber = n;
                    i++;
                }
                else if (args[i] == "--quiet") { quiet = true; }
            }

            if (chapterPath == null)
            {
                Console.Error.WriteLine("Usage: ContentAnalysisHarness.exe --chapter path\\to\\chapter.xml [--workorder path\\to\\output.xml] [--draft N] [--quiet]");
                return 1;
            }

            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;

                var analyser = new ChapterAnalyser();
                var report   = analyser.Analyse(chapterPath);

                if (!quiet)
                    Console.WriteLine(report.ToXDocument().ToString());

                if (workorderPath != null)
                {
                    var generator = new WorkOrderGenerator();
                    var workorder = generator.Generate(report, report.ChapterId, draftNumber);
                    workorder.Save(workorderPath);
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
    }
}
