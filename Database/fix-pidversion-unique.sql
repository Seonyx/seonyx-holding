-- ============================================================
-- Fix UQ_PidVersion unique constraint on ParagraphVersions
--
-- The original constraint UNIQUE (Pid, DraftNumber) prevented
-- the same PID from appearing in two different projects, causing
-- FK_Paragraphs_Chapters errors when re-importing after a
-- failed project delete left stale ParagraphVersion rows behind.
--
-- The correct constraint scopes uniqueness to a single chapter:
-- the same PID can validly appear in different chapters/projects
-- as long as it is unique within that chapter at a given draft.
--
-- Safe to run multiple times (idempotent).
-- ============================================================

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_PidVersion'
      AND object_id = OBJECT_ID(N'dbo.ParagraphVersions')
)
    ALTER TABLE dbo.ParagraphVersions DROP CONSTRAINT UQ_PidVersion;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_PidVersion'
      AND object_id = OBJECT_ID(N'dbo.ParagraphVersions')
)
    ALTER TABLE dbo.ParagraphVersions
        ADD CONSTRAINT UQ_PidVersion UNIQUE (ChapterID, Pid, DraftNumber);
