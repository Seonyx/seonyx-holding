# Claude Code Brief: Work Order Review Panel (Phase 2, Session 2)

## Context

You are adding a Work Order Review panel to the existing Draft Analysis view in the Seonyx book editor — an ASP.NET MVC application targeting .NET Framework 4.6.2. Phase 2 Session 1 added a `WorkOrderGenerator` that produces XML work order manifests. This session builds the UI for reviewing, adjusting, and exporting those manifests before handing them to Openclaw.

Read `phase2-workorder-spec.md` §6 for the full UI design. This brief scopes your work to the MVC implementation.

**Before writing any code, examine the existing Draft Analysis view and the editor's MVC conventions** — controllers, view structure, layout patterns, JavaScript approach, CSS framework. Match them. Do not introduce new frontend frameworks or libraries unless the project already uses them.

---

## What to Build

### 1. Work Order Generation Trigger

The Draft Analysis view already displays analysis results from Phase 1. Add a button: **"Generate Work Orders"**.

When clicked:
- Calls the `WorkOrderGenerator.Generate()` method with the current chapter's analysis report, chapter ID, and draft number.
- Stores the resulting manifest in memory (session or view state — match existing patterns in the app).
- Displays the Work Order Review panel below or alongside the existing analysis results.

If a manifest has already been generated or loaded for this chapter/draft, the panel displays immediately without requiring the button click.

### 2. Entry List

A sortable table displaying all work order entries. Columns:

| Column | Source | Notes |
|--------|--------|-------|
| ID | `entry/@id` | WO-001, WO-002, etc. |
| Status | `entry/@status` | Show as a toggle: pending ↔ skipped |
| Type | `entry/@type` | Display as readable label: "Outlier Word", "Repetitive N-gram", "Proximity Echo", "Manual" |
| Severity | `entry/@severity` | Numeric, one decimal place. Consider colour coding: red >80, amber 50–80, green <50 |
| Subject | `entry/subject` | The word or phrase |
| PIDs | `entry/targets/pid` count | Show count, not full list |
| Constraints | `entry/constraints` summary | Abbreviated: e.g., "Ban: corridor, passageway. Max: 5/chapter" |

Default sort: severity descending (matches the manifest's own ordering).

The user can click column headers to re-sort. The user can click the Status cell to toggle an entry between `pending` and `skipped`. Skipped entries should be visually distinct (greyed out, strikethrough, or similar — match the app's existing visual language for disabled items).

### 3. Entry Detail / Edit Panel

Clicking a row in the entry list opens a detail panel (below the list, in a side panel, or as a modal — match existing patterns in the app for detail views).

The detail panel shows:

**Read-only section:**
- Subject
- Description (the full statistical explanation)
- Type and severity

**Editable section:**

**Targets:** List of PIDs with a remove button (×) next to each. Removing a PID removes it from the entry's target list. Each PID should be a clickable link that navigates to that paragraph in the editor view (check how the existing flagged items list handles PID navigation and replicate that pattern).

**Bans:** List of `<ban>` elements. Each shows the type (word/phrase) and the banned term. The user can remove existing bans and add new ones via an input field with a type dropdown (word/phrase) and an add button.

**Limit:** If a `<limit>` element exists, show the type and value as editable fields. Allow adding a limit if none exists.

**Instruction:** The `<instruction>` text in an editable textarea. This is the free-text instruction that will be sent to the LLM.

Changes in the detail panel should update the in-memory manifest immediately. No separate "save" button for individual edits — the manifest is a working document until explicitly exported.

### 4. Manual Entry Creation

A button above or below the entry list: **"Add Manual Entry"**.

Opens the detail panel in creation mode:
- Type is fixed to `manual`.
- Status defaults to `pending`.
- Severity defaults to 50 (editable by the user).
- Subject: free text input.
- Description: free text input.
- Targets: the user selects PIDs. Implement this as a text input where the user can type/paste PID values, or — if the existing editor has a paragraph selection mechanism — integrate with that. At minimum, provide a text input that accepts comma-separated PIDs.
- Constraints: same editable ban/limit/instruction fields as the detail panel.

On confirmation, the entry is added to the in-memory manifest with the next sequential ID (WO-NNN).

### 5. Export

A button: **"Export Work Orders"**.

Serialises the current in-memory manifest (including all user edits, status changes, and manual additions) to XML and triggers a file download. The filename should default to `{chapterId}_draft{draftNumber}_workorders.xml`.

The exported XML must conform to `bookml-workorder.xsd` — same structure as the generator produces, with any user modifications applied.

### 6. Summary Bar

At the top of the Work Order Review panel, show a summary line:
- Total entries
- Pending count
- Skipped count
- Breakdown by type (e.g., "13 outlier words, 218 n-grams, 3108 echoes, 0 manual")

Update dynamically as the user toggles statuses or adds/removes entries.

---

## What NOT to Build

- No Openclaw integration or dispatch.
- No automated processing of work orders.
- No note injection into bookml-notes files.
- No modifications to the analysis engine or work order generator logic.
- No loading of previously exported work order files (nice-to-have, not in scope — the user generates fresh or works from the current session's manifest).

---

## Implementation Notes

### MVC Structure

- **Controller:** Add actions to the existing controller that handles the Draft Analysis view, or create a new `WorkOrderController` if the existing structure favours one-controller-per-concern. Match existing conventions.
- **View:** Add a partial view for the Work Order Review panel, rendered within the existing Draft Analysis view. Use `@Html.Partial` or `@Html.RenderPartial` or sections — whatever the existing views use.
- **Model:** Create a view model that wraps the work order manifest data. The `XDocument` from the generator should be mapped to a view model with typed properties rather than passing raw XML to the view.

### JavaScript

The entry list interactions (sorting, status toggling, row selection) and the detail panel edits need client-side JavaScript. Keep the in-memory manifest state client-side in a JavaScript object. The export button serialises this state to XML and triggers a download (either via a POST to a controller action that returns a file result, or client-side XML generation if the existing app has patterns for that).

Match the existing JavaScript approach — if the app uses jQuery, use jQuery. If it uses vanilla JS, use vanilla JS. Do not introduce React, Vue, or any SPA framework.

### CSS

Match the existing stylesheet. If the app has a design system or component library, use its table, button, panel, and form styles. Add minimal custom CSS only where needed.

### Validation

When the user removes the last PID from an entry's targets, warn them (the entry becomes meaningless without targets). Either prevent the removal or auto-set the entry to `skipped`.

When adding a manual entry, require at minimum: subject, at least one target PID, and instruction text.

---

## Verification

1. Build solution — zero errors.
2. Open a chapter in the Draft Analysis view. Click "Generate Work Orders." The panel appears with the entry list populated.
3. Entries are sorted by severity. Colour coding is visible.
4. Click an entry — detail panel opens with correct data.
5. Toggle an entry to "skipped" — it visually changes, summary bar updates.
6. Edit an entry's instruction text and remove a ban — changes persist in the panel.
7. Add a manual entry with subject, PIDs, and instruction — it appears in the list with the next sequential ID.
8. Click "Export Work Orders" — downloads an XML file. Open it and verify it reflects all edits, status changes, and manual additions.
9. Click a PID link in the detail panel — navigates to the paragraph in the editor.
10. Removing all PIDs from an entry triggers a warning or auto-skips the entry.
