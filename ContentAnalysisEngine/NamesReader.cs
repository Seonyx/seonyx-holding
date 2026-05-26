using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace ContentAnalysisEngine
{
    /// <summary>
    /// Reads a BookML names.xml file and returns the set of character names.
    /// </summary>
    public static class NamesReader
    {
        private static readonly XNamespace Ns = "https://bookml.org/ns/1.0";

        /// <summary>
        /// Reads character names from a names.xml file.
        /// Returns an empty set if namesFilePath is null.
        /// </summary>
        public static HashSet<string> ReadNames(string namesFilePath)
        {
            if (namesFilePath == null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var doc = XDocument.Load(namesFilePath);
            return new HashSet<string>(
                doc.Descendants(Ns + "name")
                   .Select(n => (string)n.Attribute("value"))
                   .Where(v => !string.IsNullOrWhiteSpace(v)),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
