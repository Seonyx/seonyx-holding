using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Seonyx.Web.Services
{
    // Framework-agnostic: no System.Web, no EF6, no SeonyxContext, no
    // HttpContext. Source-linked directly into RoundTripCheckCli
    // (net462) alongside MarkdownInlineParser and
    // RunsToMarkdownSerializer.

    /// <summary>
    /// A plain, DB-independent representation of one run's style state,
    /// used as the common currency between XML walking and the
    /// Markdown parser/serializer. Callers convert to/from
    /// Models.ParagraphRun at the DB boundary.
    /// </summary>
    public class InlineRun
    {
        public string Text { get; set; }
        public bool IsEmph { get; set; }
        public string EmphType { get; set; }
        public bool IsStrong { get; set; }
        public bool IsSmallCaps { get; set; }
        public string OverlayType { get; set; }
        public bool IsBreak { get; set; }
    }

    /// <summary>
    /// Thrown when source XML violates the inline nesting rules
    /// (amendment section 2.4) -- specifically overlay-inside-overlay.
    /// </summary>
    public class InlineXmlNestingException : Exception
    {
        public string Pid { get; private set; }

        public InlineXmlNestingException(string pid, string message)
            : base(message)
        {
            Pid = pid;
        }
    }

    /// <summary>
    /// Converts between a paragraph's mixed-content XML (emph/strong/
    /// smallcaps/overlay/linebreak, arbitrarily nested per amendment
    /// section 2.4) and a flat, ordered list of leaf InlineRun objects
    /// (amendment section 3.3). Both directions live here so the
    /// flatten/emit algorithms stay next to each other as inverses.
    ///
    /// Inheritance down the nesting chain, on the walk-and-flatten
    /// direction:
    ///   - IsEmph/IsStrong/IsSmallCaps accumulate via OR -- a leaf run's
    ///     flag is true if it or any ancestor set it.
    ///   - OverlayType is value-inheritance, not OR: any run inside
    ///     &lt;overlay type="X"&gt; has OverlayType = "X" regardless of its
    ///     own inline style flags, and all descendants inherit that
    ///     same value regardless of further nesting.
    ///   - Nested &lt;overlay&gt; inside &lt;overlay&gt; is a validation error
    ///     (throws InlineXmlNestingException), not a silent overwrite.
    /// </summary>
    public static class InlineXmlRunConverter
    {
        private static readonly XNamespace Ns = "https://bookml.org/ns/1.0";

        // ------------------------------------------------------------
        // XML -> runs (import path)
        // ------------------------------------------------------------

        public static IList<InlineRun> FlattenToRuns(XElement paraEl, string pid)
        {
            var runs = new List<InlineRun>();
            var state = new RunState();
            Walk(paraEl, state, runs, pid);
            return runs;
        }

        private class RunState
        {
            public bool IsEmph;
            public string EmphType;
            public bool IsStrong;
            public bool IsSmallCaps;
            public string OverlayType;

            public RunState Clone()
            {
                return new RunState
                {
                    IsEmph = IsEmph,
                    EmphType = EmphType,
                    IsStrong = IsStrong,
                    IsSmallCaps = IsSmallCaps,
                    OverlayType = OverlayType
                };
            }
        }

        private static void Walk(XElement el, RunState state, List<InlineRun> runs, string pid)
        {
            foreach (var node in el.Nodes())
            {
                if (node is XText textNode)
                {
                    if (textNode.Value.Length == 0)
                        continue;

                    runs.Add(new InlineRun
                    {
                        Text = textNode.Value,
                        IsEmph = state.IsEmph,
                        EmphType = state.EmphType,
                        IsStrong = state.IsStrong,
                        IsSmallCaps = state.IsSmallCaps,
                        OverlayType = state.OverlayType,
                        IsBreak = false
                    });
                    continue;
                }

                if (!(node is XElement child))
                    continue;

                string localName = child.Name.LocalName;

                if (localName == "linebreak")
                {
                    runs.Add(new InlineRun
                    {
                        Text = null,
                        IsEmph = state.IsEmph,
                        EmphType = state.EmphType,
                        IsStrong = state.IsStrong,
                        IsSmallCaps = state.IsSmallCaps,
                        OverlayType = state.OverlayType,
                        IsBreak = true
                    });
                    continue;
                }

                if (localName == "emph")
                {
                    var next = state.Clone();
                    next.IsEmph = true;
                    next.EmphType = (string)child.Attribute("type");
                    Walk(child, next, runs, pid);
                    continue;
                }

                if (localName == "strong")
                {
                    var next = state.Clone();
                    next.IsStrong = true;
                    Walk(child, next, runs, pid);
                    continue;
                }

                if (localName == "smallcaps")
                {
                    var next = state.Clone();
                    next.IsSmallCaps = true;
                    Walk(child, next, runs, pid);
                    continue;
                }

                if (localName == "overlay")
                {
                    if (state.OverlayType != null)
                    {
                        throw new InlineXmlNestingException(pid, string.Format(
                            "Paragraph {0}: <overlay> nested inside another <overlay> is not allowed.", pid));
                    }

                    var next = state.Clone();
                    next.OverlayType = (string)child.Attribute("type") ?? "";
                    Walk(child, next, runs, pid);
                    continue;
                }

                // Unknown/other element (e.g. footnote-ref): treat its
                // flattened text as plain content at the current style,
                // consistent with how the rest of the pipeline already
                // tolerates unrelated inline elements via .Value.
                string flat = child.Value;
                if (!string.IsNullOrEmpty(flat))
                {
                    runs.Add(new InlineRun
                    {
                        Text = flat,
                        IsEmph = state.IsEmph,
                        EmphType = state.EmphType,
                        IsStrong = state.IsStrong,
                        IsSmallCaps = state.IsSmallCaps,
                        OverlayType = state.OverlayType,
                        IsBreak = false
                    });
                }
            }
        }

        // ------------------------------------------------------------
        // Runs -> XML (export path)
        // ------------------------------------------------------------

        /// <summary>
        /// Builds the mixed-content child nodes for a paragraph element
        /// from an ordered run list, per amendment section 5.1:
        /// deterministic nesting (strong outermost, then emph, then
        /// smallcaps), overlay grouping by consecutive runs sharing the
        /// same non-null OverlayType, linebreak runs emit
        /// &lt;linebreak/&gt; with no text.
        /// </summary>
        public static IList<object> BuildParaContent(IList<InlineRun> runs)
        {
            var content = new List<object>();
            int i = 0;

            while (i < runs.Count)
            {
                var run = runs[i];

                if (run.OverlayType != null)
                {
                    // Group consecutive runs sharing this OverlayType.
                    int j = i;
                    var group = new List<InlineRun>();
                    while (j < runs.Count && runs[j].OverlayType == run.OverlayType)
                    {
                        group.Add(runs[j]);
                        j++;
                    }

                    var overlayEl = new XElement(Ns + "overlay",
                        new XAttribute("type", run.OverlayType));
                    foreach (var node in BuildStyledNodes(group))
                        overlayEl.Add(node);

                    content.Add(overlayEl);
                    i = j;
                    continue;
                }

                // Non-overlay run(s) -- emit until the next overlay
                // boundary, styled individually (no further grouping
                // needed since styled wrapping already coalesces
                // adjacent identical-style runs via BuildStyledNodes
                // being called per-run below).
                content.Add(BuildStyledNode(run));
                i++;
            }

            return content;
        }

        private static IList<object> BuildStyledNodes(IEnumerable<InlineRun> runs)
        {
            var list = new List<object>();
            foreach (var run in runs)
                list.Add(BuildStyledNode(run));
            return list;
        }

        // ------------------------------------------------------------
        // Coalescing -- shared by the importer and the editor save-side
        // sync shim, both of which must collapse adjacent identical-
        // style fragments before calling ParagraphRunService.SaveRuns
        // (which rejects coalescing violations).
        // ------------------------------------------------------------

        public static IList<InlineRun> Coalesce(IList<InlineRun> runs)
        {
            var result = new List<InlineRun>();

            foreach (var run in runs)
            {
                if (result.Count > 0 && SameStyle(result[result.Count - 1], run) && !run.IsBreak)
                {
                    var prev = result[result.Count - 1];
                    prev.Text = (prev.Text ?? "") + (run.Text ?? "");
                    continue;
                }

                result.Add(new InlineRun
                {
                    Text = run.Text,
                    IsEmph = run.IsEmph,
                    EmphType = run.EmphType,
                    IsStrong = run.IsStrong,
                    IsSmallCaps = run.IsSmallCaps,
                    OverlayType = run.OverlayType,
                    IsBreak = run.IsBreak
                });
            }

            return result;
        }

        private static bool SameStyle(InlineRun a, InlineRun b)
        {
            return a.IsEmph == b.IsEmph
                && a.EmphType == b.EmphType
                && a.IsStrong == b.IsStrong
                && a.IsSmallCaps == b.IsSmallCaps
                && a.OverlayType == b.OverlayType
                && a.IsBreak == b.IsBreak
                && !a.IsBreak;
        }

        private static object BuildStyledNode(InlineRun run)
        {
            if (run.IsBreak)
                return new XElement(Ns + "linebreak");

            object node = new XText(run.Text ?? "");

            // Deterministic nesting: strong outermost, then emph, then
            // smallcaps (amendment section 5.1).
            if (run.IsSmallCaps)
                node = new XElement(Ns + "smallcaps", node);

            if (run.IsEmph)
            {
                var emphEl = new XElement(Ns + "emph", node);
                if (!string.IsNullOrEmpty(run.EmphType))
                    emphEl.Add(new XAttribute("type", run.EmphType));
                node = emphEl;
            }

            if (run.IsStrong)
                node = new XElement(Ns + "strong", node);

            return node;
        }
    }
}
