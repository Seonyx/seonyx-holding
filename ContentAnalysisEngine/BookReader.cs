using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    public class BookManifest
    {
        public string BookId { get; set; }
        public string Title  { get; set; }
        public List<BookChapterRef> Chapters { get; set; } = new List<BookChapterRef>();
    }

    public class BookChapterRef
    {
        public string ComponentId      { get; set; }
        public string ChapterFilePath  { get; set; }  // absolute path
    }

    public static class BookReader
    {
        private static readonly XNamespace Ns = "https://bookml.org/ns/1.0";

        public static BookManifest ReadManifest(string bookXmlPath)
        {
            if (!File.Exists(bookXmlPath))
                throw new FileNotFoundException("book.xml not found.", bookXmlPath);

            var doc      = XDocument.Load(bookXmlPath);
            var root     = doc.Root;
            var baseDir  = Path.GetDirectoryName(Path.GetFullPath(bookXmlPath));

            var manifest = new BookManifest
            {
                BookId = (string)root?.Attribute("id") ?? "",
                Title  = (string)root?.Element(Ns + "bookinfo")?.Element(Ns + "title") ?? ""
            };

            var bodymatter = root
                ?.Element(Ns + "contents")
                ?.Element(Ns + "bodymatter");

            if (bodymatter == null)
                return manifest;

            var components = bodymatter
                .Elements(Ns + "component")
                .OrderBy(c => { int s; return int.TryParse((string)c.Attribute("seq"), out s) ? s : 0; })
                .ToList();

            foreach (var comp in components)
            {
                string chapterFile = (string)comp.Attribute("chapter-file");
                if (string.IsNullOrEmpty(chapterFile)) continue;

                string absPath = Path.GetFullPath(Path.Combine(baseDir, chapterFile));
                manifest.Chapters.Add(new BookChapterRef
                {
                    ComponentId     = (string)comp.Attribute("id") ?? "",
                    ChapterFilePath = absPath
                });
            }

            return manifest;
        }
    }
}
