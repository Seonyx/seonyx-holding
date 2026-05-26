# Character Sketch Add-on — Specification v1.0

**Module:** Character Sketch
**Target:** Book Editor application (SQL Server / .NET 4.8 / IIS)
**Scope:** Phase 1 — Core profiles, aliases, and tags
**Author:** Steve (spec prepared for Claude Code handoff)

---

## 1. Overview

A character management add-on for the Book Editor. Characters are stored per-book in the existing SQL Server database. The UI is a standalone `.aspx` page linked from the editor's navigation.

Phase 1 delivers: character profiles with structured and narrative fields, multiple aliases per character, and a free-form tagging system for filtering. The schema is designed so that scene linkage, relationships, knowledge tracking, and arc/change-log tables can be added in later phases without refactoring.

---

## 2. Database Design

### 2.1 ID Strategy

All new tables use `VARCHAR(40)` primary keys, app-assigned. Format: descriptive prefix + short token.

Examples:
- `char_marnette_croft`
- `alias_marnette_001`
- `tag_marnette_001`

This matches the existing Book Editor convention of avoiding `IDENTITY` columns for portability and import/export clarity.

### 2.2 Book Scoping

Every character row carries a `BookId` foreign key to the existing `Book` table so that character data is partitioned per book. All queries filter on `BookId`.

### 2.3 Tables

#### 2.3.1 `Character`

The master record. Structured fields at the top; long-form narrative fields kept directly on the row (not micro-normalised).

```sql
CREATE TABLE Character (
    CharacterId              VARCHAR(40)   NOT NULL PRIMARY KEY,
    BookId                   INT           NOT NULL,  -- FK to existing Book table

    -- Identity
    Name                     NVARCHAR(200) NOT NULL,
    Pronouns                 NVARCHAR(100) NULL,
    AgeDisplay               NVARCHAR(50)  NULL,      -- e.g. "late 30s", "142"
    DateOfBirth              DATE          NULL,
    SpeciesType              NVARCHAR(100) NULL,

    -- Classification (plain text codes, validated in app)
    StatusCode               VARCHAR(40)   NULL,      -- alive|dead|missing|unknown|inactive
    StoryRoleCode            VARCHAR(40)   NOT NULL,   -- protagonist|antagonist|ally|mentor|foil|rival|supporting|background
    ImportanceCode           VARCHAR(40)   NOT NULL,   -- major|secondary|minor|mentioned_only
    SpoilerLevelCode         VARCHAR(40)   NULL,      -- none|mild|moderate|major|full
    IsPov                    BIT           NOT NULL DEFAULT 0,

    -- Location & faction (plain text for Phase 1; FK-able later)
    HomeLocation             NVARCHAR(200) NULL,
    CurrentLocation          NVARCHAR(200) NULL,
    Faction                  NVARCHAR(200) NULL,

    -- Occupation
    Occupation               NVARCHAR(200) NULL,
    ClassStatus              NVARCHAR(200) NULL,

    -- Psychology (narrative TEXT fields — do not normalise)
    Goal                     NVARCHAR(MAX) NULL,
    Need                     NVARCHAR(MAX) NULL,
    Motivation               NVARCHAR(MAX) NULL,
    Stakes                   NVARCHAR(MAX) NULL,
    Fear                     NVARCHAR(MAX) NULL,
    Flaw                     NVARCHAR(MAX) NULL,
    Strength                 NVARCHAR(MAX) NULL,
    LieTheyBelieve           NVARCHAR(MAX) NULL,
    CoreValue                NVARCHAR(MAX) NULL,
    InternalConflict         NVARCHAR(MAX) NULL,
    ExternalConflict         NVARCHAR(MAX) NULL,
    Secret                   NVARCHAR(MAX) NULL,
    LineTheyWontCross        NVARCHAR(MAX) NULL,

    -- Appearance & behaviour
    PhysicalDescription      NVARCHAR(MAX) NULL,
    DistinctiveFeatures      NVARCHAR(MAX) NULL,
    ClothingProps            NVARCHAR(MAX) NULL,
    VoicePattern             NVARCHAR(MAX) NULL,
    HabitsMannerisms         NVARCHAR(MAX) NULL,
    BodyLanguage             NVARCHAR(MAX) NULL,
    PublicPersona            NVARCHAR(MAX) NULL,
    PrivateSelf              NVARCHAR(MAX) NULL,

    -- Background
    EducationTraining        NVARCHAR(MAX) NULL,
    OriginBackground         NVARCHAR(MAX) NULL,
    ImportantPastEvents      NVARCHAR(MAX) NULL,
    FamilyNotes              NVARCHAR(MAX) NULL,
    WeaknessesLimitations    NVARCHAR(MAX) NULL,
    HealthInjuries           NVARCHAR(MAX) NULL,

    -- Story-level notes
    StoryArcSummary          NVARCHAR(MAX) NULL,
    CharacterFunction        NVARCHAR(MAX) NULL,
    RelationshipSummary      NVARCHAR(MAX) NULL,
    ContinuityNotes          NVARCHAR(MAX) NULL,
    UnresolvedThreads        NVARCHAR(MAX) NULL,
    Notes                    NVARCHAR(MAX) NULL,
    ReferenceImageUri        NVARCHAR(500) NULL,

    -- Audit
    CreatedAt                DATETIME2     NOT NULL DEFAULT GETDATE(),
    UpdatedAt                DATETIME2     NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Character_Book FOREIGN KEY (BookId) REFERENCES Book(BookId)
);

CREATE INDEX IX_Character_BookId ON Character (BookId);
```

#### 2.3.2 `CharacterAlias`

One row per alias, nickname, title, codename, or birth name.

```sql
CREATE TABLE CharacterAlias (
    CharacterAliasId         VARCHAR(40)   NOT NULL PRIMARY KEY,
    CharacterId              VARCHAR(40)   NOT NULL,
    Alias                    NVARCHAR(200) NOT NULL,
    AliasTypeCode            VARCHAR(40)   NULL,      -- nickname|title|codename|birth_name|public_name
    SortOrder                INT           NULL,
    Notes                    NVARCHAR(MAX) NULL,

    CONSTRAINT FK_CharAlias_Character FOREIGN KEY (CharacterId) REFERENCES Character(CharacterId) ON DELETE CASCADE
);
```

#### 2.3.3 `CharacterTag`

Free-form tags for filtering. Unique per character.

```sql
CREATE TABLE CharacterTag (
    CharacterTagId           VARCHAR(40)   NOT NULL PRIMARY KEY,
    CharacterId              VARCHAR(40)   NOT NULL,
    Tag                      NVARCHAR(100) NOT NULL,

    CONSTRAINT FK_CharTag_Character FOREIGN KEY (CharacterId) REFERENCES Character(CharacterId) ON DELETE CASCADE
);

CREATE UNIQUE INDEX UX_CharacterTag_CharTag ON CharacterTag (CharacterId, Tag);
```

### 2.4 Code Values (App-Validated)

Do **not** create lookup tables in Phase 1. Store codes as plain `VARCHAR(40)` and validate in the .NET layer using static lists. The valid values are:

| Field | Values |
|---|---|
| `StatusCode` | alive, dead, missing, unknown, inactive |
| `StoryRoleCode` | protagonist, antagonist, deuteragonist, ally, mentor, foil, rival, authority, supporting, background |
| `ImportanceCode` | major, secondary, minor, mentioned_only |
| `SpoilerLevelCode` | none, mild, moderate, major, full |
| `AliasTypeCode` | nickname, title, codename, birth_name, public_name |

---

## 3. Server Layer (.NET 4.8)

### 3.1 Data Access

Use the existing Book Editor DAL pattern (ADO.NET with parameterised queries against the SQL Server connection).

Provide a `CharacterRepository` class with:

| Method | Purpose |
|---|---|
| `GetAllForBook(int bookId)` | Returns list of character summaries (Id, Name, StoryRoleCode, ImportanceCode, IsPov, StatusCode) |
| `GetById(string characterId)` | Full character record + aliases + tags |
| `Save(Character entity)` | Insert or update (upsert by CharacterId). Sets `UpdatedAt`. |
| `Delete(string characterId)` | Cascading delete (aliases & tags follow via FK) |
| `GetTagsInUse(int bookId)` | Distinct tag list for the filter panel |

### 3.2 API Endpoints

Expose as `.ashx` handlers (matching existing Book Editor pattern) or as WebAPI controllers if the project already uses WebAPI:

| Endpoint | Verb | Payload |
|---|---|---|
| `api/characters?bookId={id}` | GET | — |
| `api/characters/{characterId}` | GET | — |
| `api/characters` | POST | JSON character object (aliases and tags nested) |
| `api/characters/{characterId}` | PUT | JSON character object |
| `api/characters/{characterId}` | DELETE | — |
| `api/characters/tags?bookId={id}` | GET | — |

JSON shape for POST/PUT — aliases and tags are submitted inline:

```json
{
  "characterId": "char_marnette_croft",
  "bookId": 1,
  "name": "Marnette Croft",
  "storyRoleCode": "protagonist",
  "importanceCode": "major",
  "isPov": true,
  "goal": "Exploit the Voyager signal before anyone else discovers it",
  "aliases": [
    { "characterAliasId": "alias_marnette_001", "alias": "Nette", "aliasTypeCode": "nickname", "sortOrder": 1 }
  ],
  "tags": ["engineer", "madrid", "pov"]
}
```

Save logic: on PUT/POST the server replaces the full alias and tag sets (delete-and-reinsert within a transaction). This avoids differential child-row patching.

---

## 4. UI — Standalone Page

### 4.1 Page Structure

A single `.aspx` page (or `.html` + JS if the editor already uses client-side rendering), linked from the editor nav bar. The page has two panels:

```
┌─────────────────────────────────────────────────┐
│  Character Sketch              [+ New Character] │
├──────────────┬──────────────────────────────────┤
│              │                                  │
│  LIST PANEL  │         DETAIL PANEL             │
│              │                                  │
│  Search: [__]│  ┌─────────────────────────────┐ │
│              │  │ Identity section             │ │
│  Filter by:  │  │ Classification section       │ │
│  Role  [ v ] │  │ Psychology section           │ │
│  Import [ v ]│  │ Appearance section           │ │
│  Tag   [ v ] │  │ Background section           │ │
│  Status [ v ]│  │ Story Notes section          │ │
│              │  │ Aliases sub-panel            │ │
│  ──────────  │  │ Tags sub-panel               │ │
│  Marnette ★  │  │                              │ │
│  Pablo       │  │        [Save] [Delete]        │ │
│  Frost       │  └─────────────────────────────┘ │
│              │                                  │
└──────────────┴──────────────────────────────────┘
```

### 4.2 List Panel

- Shows all characters for the current book, sorted alphabetically.
- ★ icon for POV characters.
- Text search filters on Name + aliases.
- Dropdown filters for StoryRoleCode, ImportanceCode, StatusCode, and Tag (populated from `GetTagsInUse`).
- Clicking a row loads the detail panel.

### 4.3 Detail Panel

Organised into collapsible sections matching the field groups in the table:

| Section | Fields |
|---|---|
| **Identity** | Name, Pronouns, AgeDisplay, DateOfBirth, SpeciesType, Occupation, ClassStatus, ReferenceImageUri |
| **Classification** | StoryRoleCode (dropdown), ImportanceCode (dropdown), StatusCode (dropdown), SpoilerLevelCode (dropdown), IsPov (checkbox) |
| **Location & Faction** | HomeLocation, CurrentLocation, Faction |
| **Psychology** | Goal, Need, Motivation, Stakes, Fear, Flaw, Strength, LieTheyBelieve, CoreValue, InternalConflict, ExternalConflict, Secret, LineTheyWontCross |
| **Appearance** | PhysicalDescription, DistinctiveFeatures, ClothingProps, VoicePattern, HabitsMannerisms, BodyLanguage, PublicPersona, PrivateSelf |
| **Background** | EducationTraining, OriginBackground, ImportantPastEvents, FamilyNotes, WeaknessesLimitations, HealthInjuries |
| **Story Notes** | StoryArcSummary, CharacterFunction, RelationshipSummary, ContinuityNotes, UnresolvedThreads, Notes |
| **Aliases** | Editable list: Alias text, AliasTypeCode dropdown, SortOrder. Add/remove rows. |
| **Tags** | Token input (type and press Enter to add; click × to remove). |

All narrative fields render as `<textarea>` with reasonable height (3–5 rows). Dropdowns for code fields use the value lists from §2.4.

### 4.4 Behaviour

- **Save** sends the full object (including nested aliases/tags) as a single POST or PUT.
- **New Character** clears the detail panel and generates a new `CharacterId` client-side (format: `char_` + slugified name + `_` + 6-char random token, e.g. `char_pablo_ruiz_k7q2x1`).
- **Delete** requires confirmation dialog.
- **Unsaved changes** — warn before navigating away or selecting a different character.
- **Validation** — Name, StoryRoleCode, and ImportanceCode are required. Highlight missing fields on save attempt.

---

## 5. Future Phases (Out of Scope — Design Only)

The schema is designed so these tables can be added without altering the Phase 1 tables:

| Phase | Tables | Purpose |
|---|---|---|
| 2 | `Location`, `Faction`, `Scene` + FKs on `Character` | Replace free-text location/faction with structured references; enable scene linkage |
| 3 | `CharacterSceneAppearance`, `CharacterSceneMention` | Track which characters appear or are mentioned in each scene |
| 4 | `CharacterRelationship` | Structured relationships with type, status, intensity, visibility |
| 5 | `CharacterKnowledgeEvent` | Per-character fact tracking for continuity (who knows what, when) |
| 6 | `CharacterArcEvent`, `CharacterChangeEvent` | Arc beats and physical/status change log |

When Phase 2 lands, `HomeLocation`, `CurrentLocation`, and `Faction` on `Character` become nullable FKs instead of free text. Migration script converts existing text values into lookup rows.

---

## 6. Acceptance Criteria

1. Running the DDL script against the Book Editor database creates the three tables and indexes without errors.
2. The API returns an empty character list for a book with no characters.
3. Creating a character with name, role, importance, two aliases, and three tags round-trips correctly (POST then GET returns identical data).
4. The list panel filters correctly by role, importance, status, tag, and text search.
5. Deleting a character cascades to its aliases and tags.
6. The detail panel warns on unsaved changes before navigation.
7. Code fields reject values not in the §2.4 lists (server-side validation returns 400).

---

## 7. Delivery

- `CharacterSketch_DDL.sql` — table creation script
- `CharacterRepository.cs` — data access class
- API handler(s)
- `CharacterSketch.aspx` (or `.html` + `.js`) — the UI page
- Navigation link added to the editor layout
