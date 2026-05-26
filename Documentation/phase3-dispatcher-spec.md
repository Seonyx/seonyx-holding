# Phase 3: Openclaw Dispatcher Template

## Specification for BookML Analytics Add-on

**Version:** 1.0
**Date:** 2026-04-04
**Status:** Design-ready
**Dependencies:** Phase 1 (Content Analysis Engine + CLI wrapper), Phase 2 (Work Order Schema + Manifests), Openclaw platform, .NET runtime on Linux Mint

---

## 1. Purpose

Phase 3 creates an Openclaw skill that consumes work order manifests (Phase 2) and dispatches remediation tasks to LLMs in a controlled, resumable, state-machine-driven workflow. The skill reads a manifest, processes entries in severity order, constructs minimal prompts from the constraints, validates rewrites using the deterministic analysis engine CLI, and writes the revised chapter XML to an output folder without ever modifying the input.

### 1.1 What This Phase Delivers

- An Openclaw skill (`bookml-remediation`) installed in `~/.openclaw/workspace/skills/bookml-remediation/`.
- A `SKILL.md` that defines the skill's capabilities, inputs, and state-machine workflow.
- A state file protocol (`state.xml`) for resumable, completion-gated processing.
- Deterministic validation via the Phase 1 CLI wrapper (`content-analyser`) invoked as a subprocess.
- Draft-increment logic that produces a correctly versioned BookML output package.

### 1.2 What This Phase Does Not Deliver

- No token budget management or automatic batch sizing (Phase 4).
- No model ladder selection logic — the model is specified in the task instruction by the operator. Phase 4 adds automatic model routing based on entry type and budget.
- No re-analysis loop (running analysis → generating new work orders → processing them in the same job). One manifest in, one revised draft out.

---

## 2. Deployment Topology

The workflow spans three machines:

- **Cloud Windows Server** — Hosts the Seonyx book editor. The operator exports BookML packages from here for local processing, and imports revised packages back as new drafts. No Openclaw, no CLI tools on this machine.
- **Dibbler (Windows, development)** — Development machine where Claude Code builds the Seonyx codebase and the cross-platform CLI. Not involved in production execution.
- **Binky (Linux Mint, production)** — Runs Openclaw and the content-analyser CLI. All remediation processing happens here. Receives exported BookML packages from the cloud, processes them against LLMs via Openclaw, produces output packages for re-import to the cloud.

The workflow cycle is: Cloud export → Binky processing → Cloud import.

---

## 3. Design Principles

### 3.1 Never Modify Input

The input folder contains the BookML package (book.xml, chapter files, metadata, notes) and the work order manifest. These files are read-only. All output goes to a separate output folder specified at job start. This matches the existing Openclaw convention for Steve's revision workflows.

### 3.2 Completion-Gated State Machine

The cron scheduling fragility — where the agent treats a status message as a completed turn and exits — is addressed by making every step a filesystem checkpoint. The skill does not emit conversational output until the job is complete. Progress is recorded in `state.xml` in the output folder. If the agent exits mid-job, the state file records exactly where it stopped, and the next invocation resumes from that point.

### 3.3 Small Prompt, Deterministic Validation

Each LLM call sends the minimum context needed: the target paragraph(s), the constraint instructions, and nothing else. No statistical data, no Z-scores, no full chapter context unless a specific entry type requires it. Validation is performed by the analysis engine CLI — a zero-token, deterministic check that the rewrite satisfies the constraints (banned words removed, word counts within tolerance, XML still valid).

### 3.4 Process by Type, Then by Severity

Within a manifest, entries are processed in this order:
1. `outlier-word` entries (highest to lowest severity)
2. `repetitive-ngram` entries (highest to lowest severity)
3. `proximity-echo` entries (highest to lowest severity)

This ordering is deliberate. Fixing outlier words and n-grams often resolves proximity echoes as a side effect. Processing echoes last avoids spending tokens on problems that no longer exist. Before processing the echo batch, the skill re-validates the chapter against the echo entries and skips any that are no longer present in the text.

### 3.5 One Entry, One LLM Call (Default)

Each work order entry results in one LLM call containing only that entry's target paragraphs and constraints. This keeps prompts small, validation simple, and failure isolation tight — a failed rewrite for one entry does not affect others.

Exception: if multiple entries share the same target PIDs, they may be batched into a single call with combined constraints to avoid conflicting rewrites of the same paragraph. The skill detects this overlap and merges entries when safe to do so.

### 3.6 Rate-Limit Avoidance (Never Hit, Never Recover)

Low-cost AI subscriptions impose rate limits with escalating penalties — hitting a limit on OpenAI can double the timeout from 2 hours to 4, then 8, and so on. The skill must never trigger a rate limit. Prevention, not recovery.

Two mechanisms enforce this:

**Inter-call delay:** A configurable pause between consecutive LLM calls (default: 30 seconds). This prevents rapid-fire API requests within a single run. The delay is long enough to stay well under per-minute rate limits on low-cost tiers.

**Max entries per run:** A configurable cap on how many work order entries the skill processes in a single invocation (default: 5). After processing this many entries, the skill checkpoints to state.xml and exits cleanly, regardless of whether more entries remain. The next cron invocation resumes from the checkpoint.

The combination of cron interval (spacing between runs) and max-entries-per-run (volume per run) gives the operator precise control over API usage density. For a 2-hour cron interval with 5 entries per run, that's 5 LLM calls every 2 hours — comfortably under any subscription tier's rate limit.

These parameters are specified in the invocation message and recorded in state.xml so they persist across resume cycles.

---

## 4. Skill Structure

### 4.1 File Layout

```
~/.openclaw/workspace/skills/bookml-remediation/
    SKILL.md                    <- Skill definition and agent instructions
    lib/
        parse-manifest.ts       <- XML manifest parser
        build-prompt.ts         <- Prompt constructor per entry type
        validate-rewrite.ts     <- Calls content-analyser CLI for validation
        update-chapter.ts       <- Applies validated rewrites to chapter XML
        state-manager.ts        <- Reads/writes state.xml
        draft-incrementer.ts    <- Builds output package with incremented draft
```

### 4.2 SKILL.md Overview

The SKILL.md instructs the agent in the state-machine protocol. It is not a conversational skill — it is a batch-processing skill that runs to completion and only emits output at the end.

Key sections of SKILL.md:
- **Trigger:** "Process a BookML work order manifest for chapter remediation."
- **Inputs:** Input folder path, output folder path, model identifier, work order manifest filename.
- **State protocol:** Read `state.xml` from output folder. If it exists and is incomplete, resume. If it doesn't exist, initialise.
- **Processing loop:** The numbered gate sequence (see §4).
- **Completion:** Only valid termination is `state.xml` updated with `completedAt` and all entries resolved.
- **Failure:** If any gate fails, write error to `state.xml`, do not advance, do not emit completion.

---

## 5. State Machine

### 5.1 State File (`state.xml`)

Written to the output folder. Namespace: `https://bookml.org/ns/workorder/1.0` (shared with the manifest schema).

```xml
<?xml version="1.0" encoding="UTF-8"?>
<job-state
    xmlns="https://bookml.org/ns/workorder/1.0"
    manifest="ch01_draft3_workorders.xml"
    chapter-id="CH01-C-XXXXXXXX"
    source-draft="3"
    target-draft="4"
    model="openai/gpt-5.2"
    max-entries-per-run="5"
    inter-call-delay-seconds="30"
    started-at="2026-04-04T15:00:00Z"
    completed-at=""
    status="in-progress">

  <gate id="1" name="initialise" status="completed" timestamp="2026-04-04T15:00:01Z"/>
  <gate id="2" name="process-outlier-words" status="in-progress" timestamp="2026-04-04T15:00:02Z">
    <entry ref="WO-003" status="completed"/>
    <entry ref="WO-007" status="completed"/>
    <entry ref="WO-012" status="in-progress"/>
  </gate>
  <gate id="3" name="process-ngrams" status="pending"/>
  <gate id="4" name="revalidate-echoes" status="pending"/>
  <gate id="5" name="process-echoes" status="pending"/>
  <gate id="6" name="build-output-package" status="pending"/>
  <gate id="7" name="finalise" status="pending"/>

</job-state>
```

### 5.2 Gate Sequence

**Gate 1 — Initialise:**
- Verify input folder exists and contains the manifest and chapter XML.
- Parse the manifest. Count pending entries by type.
- Copy the chapter XML to the output folder as the working copy (all modifications happen to this copy).
- Write initial `state.xml` with all gates set to `pending` except gate 1 which is `completed`.
- If `state.xml` already exists with `status="in-progress"`, skip to the earliest incomplete gate (resume path).

**Gate 2 — Process Outlier Words:**
- Filter manifest entries where `type="outlier-word"` and `status="pending"`.
- Sort by severity descending.
- For each entry:
  1. Check the run's entry counter against `max-entries-per-run`. If the limit is reached, checkpoint state.xml and exit cleanly. The next invocation resumes here.
  2. If this is not the first LLM call in this run, wait `inter-call-delay-seconds` before proceeding.
  3. Extract target paragraphs from the working chapter XML by PID.
  4. Build prompt from the entry's `<constraints>` (see §6).
  5. Send to LLM. Receive rewritten paragraphs.
  6. Validate: call `content-analyser` CLI to check banned words are absent from the rewrite. Verify XML is well-formed. Verify paragraph count and PID integrity are preserved.
  7. If validation passes: apply rewrite to working chapter XML. Update entry status to `completed` in state.xml. Increment the run's entry counter.
  8. If validation fails: log the failure, set entry status to `failed` in state.xml, move to next entry. Do not retry (Phase 4 adds retry logic with budget awareness). Failed entries do not count against the per-run limit.
- When all outlier-word entries are processed (or the per-run limit is reached), set gate 2 to `completed` or leave it `in-progress` as appropriate.

**Note:** The per-run entry limit applies across all gates. If the skill processes 3 outlier words and hits the limit of 5 with 2 remaining, it moves to gate 3 on the next invocation only after finishing gate 2. The counter tracks LLM calls, not entries — skipped and failed entries don't count.

**Gate 3 — Process N-grams:**
- Same as gate 2 but for `type="repetitive-ngram"` entries.

**Gate 4 — Revalidate Echoes:**
- Run `content-analyser` against the working chapter XML (which now has outlier word and n-gram fixes applied).
- For each `proximity-echo` entry in the manifest, check whether the echo still exists in the updated text.
- If the echo is gone (resolved as a side effect), set the entry status to `skipped` in state.xml with a note: "Resolved by prior remediation."
- This gate produces no LLM calls. It is purely deterministic.

**Gate 5 — Process Echoes:**
- Process remaining `proximity-echo` entries that survived gate 4 (status still `pending`).
- Same process as gate 2: extract, prompt, validate, apply.

**Gate 6 — Build Output Package:**
- Construct the output BookML package in the output folder:
  - Copy `book.xml` from input, adding a new versioning entry for the target draft number with `status="in-progress"` and `based-on="{source-draft}"`.
  - Update the working chapter XML: set `draft="{target-draft}"` on the chapter root and all component entries.
  - Copy all non-chapter files (metadata, notes, other chapters) from input unchanged.
- Verify the output package is structurally complete.

**Gate 7 — Finalise:**
- Set `completed-at` timestamp in state.xml.
- Set state status to `completed`.
- Write a summary to state.xml:
  ```xml
  <summary>
    <total-entries>25</total-entries>
    <completed>18</completed>
    <failed>2</failed>
    <skipped>5</skipped>
    <elapsed-minutes>12</elapsed-minutes>
  </summary>
  ```
- Only now may the agent emit a completion message.

### 5.3 Resume Protocol

If the agent exits or crashes mid-job:
1. Next invocation checks for `state.xml` in the output folder.
2. If found and `status="in-progress"`, read the gate sequence.
3. Find the first gate that is not `completed`.
4. Within that gate, find the first entry that is not `completed` or `failed` or `skipped`.
5. Resume from that entry.

The working chapter XML in the output folder reflects all completed rewrites. No work is lost.

---

## 6. Prompt Construction

### 6.1 Prompt Template — Outlier Word

```
You are editing a fiction manuscript. Rewrite the following paragraph(s) to eliminate
or reduce usage of the word "{word}".

Constraints:
- Do NOT use the word "{word}" in your rewrite.
{foreach ban in entry.constraints.bans}
- Do NOT use the word/phrase "{ban.term}".
{/foreach}
- Preserve the paragraph's meaning, tone, and narrative flow.
- Preserve all character names and proper nouns.
- Return ONLY the rewritten paragraph text, nothing else.
- Maintain approximately the same word count (within 20%).

{foreach target in entry.targets}
[PID: {target.pid}]
{paragraphText}

{/foreach}
```

### 6.2 Prompt Template — Repetitive N-gram

```
You are editing a fiction manuscript. Rewrite the following paragraph(s) to eliminate
the repeated phrase "{phrase}".

Constraints:
- Do NOT use the phrase "{phrase}" in your rewrite.
{foreach ban in entry.constraints.bans}
- Do NOT use the phrase "{ban.term}".
{/foreach}
- Replace with a varied alternative appropriate to the character and scene.
- No two replacement paragraphs should use the same alternative gesture/phrase.
- Preserve the paragraph's meaning, tone, and narrative flow.
- Return ONLY the rewritten paragraph text, nothing else.
- Maintain approximately the same word count (within 20%).

{foreach target in entry.targets}
[PID: {target.pid}]
{paragraphText}

{/foreach}
```

### 6.3 Prompt Template — Proximity Echo

```
You are editing a fiction manuscript. In the second paragraph below, rephrase to avoid
repeating the word "{term}" which appears in the preceding paragraph.

Constraints:
- Do NOT use the word "{term}" in the second paragraph.
- Only rewrite the second paragraph. Return the first paragraph unchanged.
- Preserve the paragraph's meaning, tone, and narrative flow.
- Return ONLY the rewritten paragraph text, nothing else.

[PID: {firstPid} — DO NOT MODIFY]
{firstParagraphText}

[PID: {secondPid} — REWRITE THIS]
{secondParagraphText}
```

### 6.4 Template Customisation

The prompt templates are stored as separate text files in the skill's `lib/` directory (or inline in SKILL.md). The operator can edit them to tune the LLM's behaviour without modifying the skill logic. The `<instruction>` text from the work order entry is appended to the base template, allowing per-entry customisation from the Phase 2 UI.

---

## 7. Validation

### 7.1 Deterministic Validation via CLI

After each LLM rewrite, the skill invokes the content-analyser CLI:

```bash
content-analyser validate \
  --original /path/to/input/chapter.xml \
  --rewritten /path/to/output/chapter-working.xml \
  --entry-id WO-003 \
  --manifest /path/to/manifest.xml
```

**Note:** This requires extending the Phase 1 CLI with a `validate` subcommand (see §9). The validate command checks:

1. **XML well-formedness:** The rewritten chapter parses as valid XML.
2. **PID integrity:** All PIDs from the original chapter are present in the rewrite. No PIDs added, no PIDs removed (unless the work order explicitly allows paragraph deletion, which none of the current entry types do).
3. **Ban compliance:** For the specified entry, all `<ban>` terms are absent from the rewritten paragraphs. Case-insensitive word boundary check.
4. **Word count tolerance:** Each rewritten paragraph is within 20% of the original's word count.
5. **Limit compliance:** If the entry has a `<limit>` constraint, the term's frequency in the full chapter is at or below the limit value.

The CLI returns exit code 0 for pass, non-zero for fail, with a brief diagnostic on stderr.

### 7.2 Validation Failure Handling

On validation failure, the skill:
1. Logs the diagnostic from the CLI.
2. Sets the entry status to `failed` in state.xml.
3. Reverts the working chapter XML to its state before this entry's rewrite was applied. (The skill keeps a pre-rewrite snapshot of affected paragraphs for this purpose.)
4. Moves to the next entry.

No retry in Phase 3. Phase 4 adds configurable retry with a budget cap.

---

## 8. Draft Increment Protocol

### 8.1 Target Draft Number

The target draft number is `source-draft + 1`. The skill reads `source-draft` from the manifest's `source-draft` attribute and computes the target.

### 8.2 Output Package Structure

The output folder contains a complete BookML package ready for import:

```
output/
    book.xml                    <- Updated versioning block
    ch01/
        ch01-chapter.xml        <- Rewritten chapter (draft attribute updated)
        ch01-meta.xml           <- Copied from input unchanged
        ch01-notes.xml          <- Copied from input unchanged
    ch02/                       <- Copied from input unchanged (not in manifest)
        ...
```

### 8.3 book.xml Updates

The skill copies `book.xml` from input and makes two changes:
1. Sets the existing in-progress draft entry's status to `snapshot`.
2. Adds a new `<draft>` entry:
```xml
<draft number="{targetDraft}" status="in-progress" based-on="{sourceDraft}"
       created-at="{timestamp}" created-by="openclaw-remediation"/>
```

### 8.4 Chapter XML Updates

The rewritten chapter XML has its root element's `draft` attribute set to the target draft number. All `<component>` entries within the chapter are similarly updated.

---

## 9. Cross-Platform CLI (Dual-Target Build)

### 9.1 The Problem

The `ContentAnalysisEngine` class library targets .NET Framework 4.6.2 because it must match the Seonyx solution, which is locked to that runtime on shared Windows hosting. .NET Framework 4.6.2 is Windows-only and does not run on Linux.

Openclaw runs on Binky (Linux Mint) and needs to invoke the analysis engine as a subprocess. This requires a Linux-native CLI binary.

### 9.2 The Solution

Build two projects against the same engine source code:

1. **`ContentAnalysisEngine.csproj`** — The existing .NET Framework 4.6.2 class library, referenced by the Seonyx editor and the Windows-only test harness on Dibbler. No changes.

2. **`ContentAnalyserCli.csproj`** — A new .NET 8 console application that includes the same `.cs` source files from `ContentAnalysisEngine/` via linked file references (`<Compile Include="..\ContentAnalysisEngine\*.cs" Link="Engine\%(Filename)%(Extension)" />`). Targets `net8.0`, publishes as a self-contained Linux deployment.

The engine source uses only standard APIs (System.Xml.Linq, LINQ, System.Math) and Newtonsoft.Json, all of which are available on both targets. No conditional compilation should be needed.

### 9.3 CLI Subcommands

The .NET 8 CLI (`content-analyser`) supports three modes:

```bash
# Full chapter analysis (Phase 1 functionality)
content-analyser analyse --chapter path/to/chapter.xml

# Work order generation (Phase 2 functionality)
content-analyser workorder --chapter path/to/chapter.xml --output path/to/workorders.xml --draft 3

# Rewrite validation (Phase 3 — new)
content-analyser validate \
  --original path/to/original-chapter.xml \
  --rewritten path/to/rewritten-chapter.xml \
  --entry-id WO-003 \
  --manifest path/to/manifest.xml
```

The `validate` subcommand:
1. Parses the manifest, finds the entry by ID.
2. Loads both chapter XMLs.
3. Runs the five checks from §7.1.
4. Exits 0 on pass, exits 1 on fail with diagnostic on stderr.

### 9.4 Deployment to Binky

Claude Code on Dibbler builds both projects. The .NET 8 CLI is published as a self-contained Linux-x64 deployment:

```bash
dotnet publish ContentAnalyserCli.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64
```

This produces a single folder containing the binary and all runtime dependencies. No .NET runtime installation is required on Binky — the binary is fully standalone. Copy the `publish/linux-x64/` folder to Binky and the Openclaw skill invokes it directly.

### 9.5 Implementation Scope

This is a Claude Code session on Dibbler that:
- Creates `ContentAnalyserCli/ContentAnalyserCli.csproj` targeting .NET 8 with linked source files from the engine.
- Implements a `Program.cs` with proper subcommand routing (`analyse`, `workorder`, `validate`).
- Implements the `validate` subcommand with the five checks from §7.1.
- Adds a `WorkOrderValidator` class to the engine source (available to both targets).
- Verifies the .NET 8 project compiles and the existing .NET Framework 4.6.2 projects are unaffected.
- Publishes a self-contained Linux-x64 build.

The `analyse` and `workorder` subcommands replicate the existing `ContentAnalysisHarness` functionality. The harness itself stays in the Seonyx solution for Windows-side testing; the new CLI is the production tool for Binky.

---

## 10. Invocation

### 10.1 Manual Invocation

The operator triggers the skill via Openclaw CLI:

```bash
openclaw agent --message "Process BookML work order manifest. \
  Input: /home/steve/cty/draft3/ \
  Output: /home/steve/cty/draft4/ \
  Manifest: ch01_draft3_workorders.xml \
  Model: openai/gpt-5.2 \
  MaxEntriesPerRun: 5 \
  InterCallDelay: 30" \
  --thinking high
```

`MaxEntriesPerRun` defaults to 5 if not specified. `InterCallDelay` (in seconds) defaults to 30. For a one-off manual run where you're watching the output and not worried about rate limits, you can increase the batch size and reduce the delay.

### 10.2 Cron Invocation

For unattended processing, a cron job runs the skill at intervals:

```bash
openclaw cron add --name "cty-remediation-ch01" \
  --message "Process BookML work order manifest. Input: /home/steve/cty/draft3/ Output: /home/steve/cty/draft4/ Manifest: ch01_draft3_workorders.xml Model: openai/gpt-5.2 MaxEntriesPerRun: 5 InterCallDelay: 30" \
  --schedule "0 */2 * * *"
```

With 5 entries per run and a 2-hour cron interval, a manifest with 25 pending entries takes 5 cron cycles (10 hours) to complete. This is slow but safe — well under any subscription tier's rate limit, with no risk of the escalating timeout penalty.

For faster processing when rate limits allow, increase `MaxEntriesPerRun` or shorten the cron interval. The parameters are tunable per job — a Claude subscription with generous rate limits might use `MaxEntriesPerRun: 20` and a 30-minute interval, while a low-cost OpenAI tier stays at 5 entries every 2 hours.

### 10.3 Multi-Chapter Sequencing

Each chapter has its own manifest and its own state file. For a full-book remediation pass, the operator creates one cron job per chapter staggered across time, or a wrapper script that invokes them sequentially. Phase 4 adds a batch controller that manages this automatically.

---

## 11. Acceptance Criteria

1. The skill processes a work order manifest against a CTY chapter and produces a revised chapter XML in the output folder.
2. All completed entries in state.xml correspond to verified rewrites where banned terms are absent.
3. Failed entries are logged with diagnostics and the chapter XML reverts their changes.
4. Skipped proximity-echo entries (resolved by prior fixes) are correctly identified in gate 4.
5. The output package imports successfully into the Seonyx editor as the next draft number.
6. If the agent is interrupted mid-job, the next invocation resumes from the correct entry without re-processing completed work.
7. state.xml accurately reflects the job's final status including summary counts.
8. The skill makes no conversational output until the run completes (either by finishing all entries or reaching the per-run limit).
9. The skill stops cleanly after processing `MaxEntriesPerRun` entries and checkpoints correctly for the next invocation to resume.
10. The inter-call delay is observed between consecutive LLM calls — no two calls are made within the configured delay window.
