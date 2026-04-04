using System;
using ContentAnalysisEngine;
using Newtonsoft.Json;

namespace ContentAnalysisHarness
{
    class Program
    {
        static int Main(string[] args)
        {
            string chapterPath = null;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--chapter")
                {
                    chapterPath = args[i + 1];
                    break;
                }
            }

            if (chapterPath == null)
            {
                Console.Error.WriteLine("Usage: ContentAnalysisHarness.exe --chapter path\\to\\chapter.xml");
                return 1;
            }

            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                var analyser = new ChapterAnalyser();
                var report   = analyser.Analyse(chapterPath);
                Console.WriteLine(JsonConvert.SerializeObject(report, Formatting.Indented));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }
    }
}
