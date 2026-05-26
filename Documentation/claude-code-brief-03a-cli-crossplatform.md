# Claude Code Brief: Cross-Platform Content Analyser CLI (Phase 3, Track A)

## Context

The ContentAnalysisEngine class library and ContentAnalysisHarness console app already exist in the Seonyx solution, targeting .NET Framework 4.6.2. They work and are not to be modified.

This session creates a **second console application** targeting .NET 8, which can be published as a self-contained Linux binary for deployment to Binky (Linux Mint) where Openclaw runs. The new CLI shares the same engine source files but compiles against the cross-platform runtime.

It also adds a `validate` subcommand that checks LLM rewrites against work order constraints — a new capability needed by the Openclaw dispatcher skill.

Read `phase3-dispatcher-spec.md` §9 for the full CLI design and §7.1 for the validation checks.

**Critical: The existing .NET Framework 4.6.2 projects must continue to build and work unchanged. Do not modify `ContentAnalysisEngine.csproj` or `ContentAnalysisHarness`. The new project sits alongside them in the solution.**

---

## What to Build

### 1. New Project: ContentAnalyserCli

**Project type:** .NET 8 console application.
**Project file:** `ContentAnalyserCli/ContentAnalyserCli.csproj`

This project does NOT have its own copy of the engine source. It links to the source files from `ContentAnalysisEngine/` using linked file references:

```xml
<ItemGroup>
  <Compile Include="..\ContentAnalysisEngine\**\*.cs" 
           Link="Engine\%(RecursiveDir)%(Filename)%(Extension)" 
           Exclude="..\ContentAnalysisEngine\obj\**;..\ContentAnalysisEngine\bin\**" />
</ItemGroup>
```

This means both projects compile the same source. Any `.cs` file added to `ContentAnalysisEngine/` in future phases is automatically included in both targets.

**NuGet dependency:** Newtonsoft.Json (the engine source uses it). Add as a .NET 8 NuGet package reference — do NOT reference the .NET Framework packages/ directory.

**Verify:** After creating the project, confirm it compiles the linked engine source without errors. If any .NET Framework 4.6.2 APIs are used that don't exist in .NET 8, flag them — but based on the engine's use of System.Xml.Linq, LINQ, System.Math, and Newtonsoft.Json, there should be no issues.

### 2. Program.cs — Subcommand Router

The CLI supports three subcommands: `analyse`, `workorder`, and `validate`.

```
content-analyser analyse --chapter path/to/chapter.xml [--quiet]
content-analyser workorder --chapter path/to/chapter.xml --output path/to/workorders.xml [--draft N]
content-analyser validate --original path/to/original.xml --rewritten path/to/rewritten.xml --entry-id WO-003 --manifest path/to/manifest.xml
```

Parse args manually (no argument parsing library needed — match the pattern used in ContentAnalysisHarness). First positional argument is the subcommand. Remaining arguments are flags.

**`analyse` subcommand:**
- Replicates `ContentAnalysisHarness` behaviour: runs `ChapterAnalyser.Analyse()`, writes the analysis report XML to stdout.
- `--quiet` suppresses stdout output.
- This exists so the Openclaw skill can run analysis on Binky without needing the Windows-only harness.

**`workorder` subcommand:**
- Runs analysis, then `WorkOrderGenerator.Generate()`, writes manifest XML to the specified output path.
- `--draft` defaults to 1 if not specified.
- Writes analysis XML to stdout unless `--quiet` is present.

**`validate` subcommand:**
- See §3 below. This is the new functionality.

All subcommands write diagnostic output to stderr and results to stdout. Exit code 0 for success, 1 for validation failure, 2 for usage/argument errors.

### 3. Validate Subcommand + WorkOrderValidator Class

**New file in engine source:** `ContentAnalysisEngine/WorkOrderValidator.cs`

This class is compiled into both the .NET 8 CLI (for Binky) and the .NET Framework 4.6.2 library (for potential future use in the editor). It has no platform-specific dependencies.

```csharp
public class WorkOrderValidator
{
    public ValidationResult Validate(
        string originalChapterPath,
        string rewrittenChapterPath,
        string manifestPath,
        string entryId)
    { ... }
}

public class ValidationResult
{
    public bool Passed { get; set; }
    public List<string> Diagnostics { get; set; } // human-readable failure reasons
}
```

**Validation checks (all five must pass):**

**Check 1 — XML well-formedness:**
Load the rewritten chapter XML via `XDocument.Load()`. If it throws, fail with diagnostic "Rewritten chapter is not well-formed XML: {exception message}".

**Check 2 — PID integrity:**
Extract all `pid` attributes from `<para>` elements in both original and rewritten chapters. Compare as sets. If any PID is missing from the rewrite, fail with "Missing PIDs: {list}". If any PID is present in the rewrite but not the original, fail with "Unexpected PIDs: {list}".

**Check 3 — Ban compliance:**
Parse the manifest XML (namespace `https://bookml.org/ns/workorder/1.0`), find the `<entry>` with matching `id` attribute. Extract all `<ban>` elements from its `<constraints>`. For each ban:
- If `type="word"`: check that the banned word does not appear as a whole word (case-insensitive) in any target PID's paragraph text in the rewritten chapter. Use word boundary detection (split on non-letter characters, compare tokens).
- If `type="phrase"`: check that the banned phrase does not appear (case-insensitive) as a substring in any target PID's paragraph text.
Fail with "Ban violation: '{term}' found in PID {pid}" for each violation.

**Check 4 — Word count tolerance:**
For each target PID, compare word count in original vs rewritten. If the rewritten paragraph's word count differs by more than 20% from the original, fail with "Word count deviation in PID {pid}: original {N}, rewritten {M} ({percent}% change)".

**Check 5 — Limit compliance:**
If the entry has a `<limit type="max-per-chapter">` constraint, count occurrences of the entry's `<subject>` word in the entire rewritten chapter (all paragraphs, not just targets). If the count exceeds the limit value, fail with "Limit exceeded: '{word}' appears {count} times, limit is {max}".

**Return:** `ValidationResult` with `Passed = true` only if all five checks pass. `Diagnostics` contains all failure messages (not just the first — report all failures).

### 4. Validate Subcommand in Program.cs

When `validate` subcommand is invoked:
1. Parse `--original`, `--rewritten`, `--entry-id`, `--manifest` arguments.
2. Verify all four paths/values are provided. Exit 2 with usage message if not.
3. Verify files exist. Exit 2 with message if not.
4. Create `WorkOrderValidator`, call `Validate()`.
5. If passed: write "PASS" to stdout, exit 0.
6. If failed: write each diagnostic to stderr, write "FAIL" to stdout, exit 1.

---

## What NOT to Build

- No modifications to `ContentAnalysisEngine.csproj` or `ContentAnalysisHarness`.
- No Openclaw skill code (that's Track B, built on Binky).
- No Linux deployment steps (just verify the project targets net8.0 and can be published).
- No UI changes.

---

## Files to Create

| File | Action |
|------|--------|
| `ContentAnalyserCli/ContentAnalyserCli.csproj` | New — .NET 8 console app with linked engine sources |
| `ContentAnalyserCli/Program.cs` | New — subcommand router |
| `ContentAnalysisEngine/WorkOrderValidator.cs` | New — validation logic (compiled by both targets) |

## Files to Modify

| File | Action |
|------|--------|
| `Seonyx.sln` | Add `ContentAnalyserCli` project |
| `ContentAnalysisEngine/ContentAnalysisEngine.csproj` | Add `WorkOrderValidator.cs` to Compile entries |
| `copy-updates.ps1` | Update with new files |

---

## Verification

1. **Build entire solution** — zero errors on all three projects (ContentAnalysisEngine 4.6.2, ContentAnalysisHarness 4.6.2, ContentAnalyserCli net8.0).
2. **Run analyse subcommand:**
   ```
   dotnet run --project ContentAnalyserCli -- analyse --chapter testdata/junk_draft2/ch01/ch01-chapter.xml
   ```
   Produces analysis XML on stdout, identical metrics to the existing harness.
3. **Run workorder subcommand:**
   ```
   dotnet run --project ContentAnalyserCli -- workorder --chapter ch01-chapter.xml --output test-workorders.xml --draft 2
   ```
   Produces valid work order manifest XML.
4. **Run validate — passing case:**
   Take a chapter, manually edit one paragraph to remove a flagged word, save as rewritten.xml. Run:
   ```
   dotnet run --project ContentAnalyserCli -- validate --original ch01-chapter.xml --rewritten rewritten.xml --entry-id WO-001 --manifest test-workorders.xml
   ```
   Should output "PASS", exit 0.
5. **Run validate — failing case:**
   Use the original chapter as both original and rewritten (no changes made). Run validate against an entry with bans. Should output "FAIL", exit 1, with ban violation diagnostics on stderr.
6. **Publish self-contained Linux build:**
   ```
   dotnet publish ContentAnalyserCli/ContentAnalyserCli.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64
   ```
   Verify the output folder is created and contains the binary. (It won't run on Windows Dibbler, but the publish step should complete.)
7. **Existing harness unchanged:** Run ContentAnalysisHarness.exe against the same test chapter — output is identical to before this session.
