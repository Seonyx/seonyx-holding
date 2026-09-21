using System;
using System.Collections.Concurrent;
using System.IO;
using System.Web;
using System.Web.Hosting;

namespace Seonyx.Web.Helpers
{
    /// <summary>
    /// Cache-busting query string for static assets (CSS/JS) that aren't
    /// covered by ASP.NET's bundling/optimization pipeline (this project
    /// uses none -- see CLAUDE.md). Without a version string, a browser
    /// can keep serving a stale cached copy of a .js/.css file indefinitely
    /// after a deploy, surviving an ordinary reload and requiring a hard
    /// refresh to pick up the new version -- exactly what happened after
    /// the P3 rich-text-editor.js fixes shipped.
    ///
    /// Returns the target file's last-write timestamp (as ticks), so the
    /// query string changes automatically whenever the file is edited --
    /// no manual version bump needed on future deploys.
    /// </summary>
    public static class AssetVersion
    {
        private static readonly ConcurrentDictionary<string, string> Cache =
            new ConcurrentDictionary<string, string>();

        public static string For(string appRelativePath)
        {
            return Cache.GetOrAdd(appRelativePath, path =>
            {
                try
                {
                    var fullPath = HostingEnvironment.MapPath(path);
                    if (fullPath == null || !File.Exists(fullPath))
                        return "0";

                    return File.GetLastWriteTimeUtc(fullPath).Ticks.ToString();
                }
                catch
                {
                    // Never let a version-lookup failure break page rendering --
                    // worst case, the asset just isn't cache-busted this request.
                    return "0";
                }
            });
        }
    }
}
