using System;
using System.IO;

namespace Seonyx.Web.Services
{
    /// <summary>
    /// The per-project overlay stylesheet (BookML Amendment 2, section 2.3):
    /// {project folder}/overlay-styles.css, bundled into every EPUB. Authors
    /// edit the file directly (FTP on production); this class only supplies
    /// the default and makes sure the file exists.
    /// </summary>
    public static class OverlayStyles
    {
        public const string FileName = "overlay-styles.css";

        // Amendment 2's mandatory baseline, with layout split between overlays
        // inside a sentence (span, stays in the line) and whole-paragraph
        // overlays (aside, a block) -- an aside is not allowed inside a
        // paragraph, and display:block on the baseline would break a sentence.
        public const string DefaultCss =
@"/* Overlay styles for this project (BookML Amendment 2).
   .overlay is the mandatory baseline: every overlay gets it, whatever its
   type, so a type with no rule of its own still renders as an overlay.
   Add .overlay-<type> rules to refine particular types. The class is the
   type lower-cased, with anything other than a-z, 0-9 and - turned into
   hyphens: ""Sponsored Alert"" becomes .overlay-sponsored-alert. */

/* Baseline - applied to every overlay regardless of type */
.overlay {
    border: 1px solid #99c;
    background: #eef;
    font-family: sans-serif;
    font-size: 0.9em;
}

/* An overlay inside a sentence stays in the line */
span.overlay {
    padding: 0 0.3em;
}

/* An overlay that is a whole paragraph is a block */
aside.overlay {
    display: block;
    padding: 0.5em 1em;
    margin: 0.5em 0;
}

aside.overlay p {
    text-indent: 0;
    margin: 0;
}

/* Starter refinements for the initial type vocabulary */
.overlay-sponsored-alert {
    background: #fed;
    border-color: #c93;
}

.overlay-hud {
    font-family: monospace;
    background: #001;
    color: #0f0;
}

.overlay-system-message {
    font-family: monospace;
    background: #f4f4f4;
    border-color: #888;
}

.overlay-subtitle {
    background: transparent;
    border: none;
    font-style: italic;
}
";

        /// <summary>
        /// The project's overlay stylesheet. If the project folder has none
        /// yet, the default is written there (so there is a file to edit) and
        /// returned. Never throws: on any file error the default is returned
        /// and warning is set.
        /// </summary>
        public static string LoadOrCreate(string projectFolderPath, out string warning)
        {
            warning = null;
            if (string.IsNullOrEmpty(projectFolderPath))
            {
                warning = "Project has no folder; using the default overlay styles.";
                return DefaultCss;
            }

            var path = Path.Combine(projectFolderPath, FileName);
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);

                Directory.CreateDirectory(projectFolderPath);
                File.WriteAllText(path, DefaultCss);
                return DefaultCss;
            }
            catch (Exception ex)
            {
                warning = "Could not read or create " + path + " (" + ex.Message + "); using the default overlay styles.";
                return DefaultCss;
            }
        }
    }
}
