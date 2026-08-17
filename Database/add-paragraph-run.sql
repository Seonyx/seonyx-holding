-- ============================================================
-- Add ParagraphRuns
--
-- BookML Inline Presentation Elements amendment, Phase P1.
-- Adds the ParagraphRuns table (ordered, styled text fragments
-- of a paragraph), backfills exactly one run per existing
-- paragraph from the current text column, then renames that
-- column to ParagraphText_Legacy (kept, not dropped -- dropping
-- is a separately-gated later migration).
--
-- Nothing reads from ParagraphRuns yet. Existing app code keeps
-- reading Paragraphs.ParagraphText_Legacy via the ParagraphText
-- EF property ([Column("ParagraphText_Legacy")]).
--
-- Safe to run multiple times (idempotent).
-- ============================================================

-- ------------------------------------------------------------
-- 1. ParagraphRuns table
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ParagraphRuns')
BEGIN
    CREATE TABLE ParagraphRuns (
        ParagraphRunID  INT           IDENTITY(1,1) PRIMARY KEY,
        ParagraphID     INT           NOT NULL,
        Seq             INT           NOT NULL,
        RunText         NVARCHAR(MAX) NULL,
        IsEmph          BIT           NOT NULL DEFAULT 0,
        EmphType        NVARCHAR(32)  NULL,
        IsStrong        BIT           NOT NULL DEFAULT 0,
        IsSmallCaps     BIT           NOT NULL DEFAULT 0,
        OverlayType     NVARCHAR(32)  NULL,
        IsBreak         BIT           NOT NULL DEFAULT 0,
        CONSTRAINT FK_ParagraphRuns_Paragraphs
            FOREIGN KEY (ParagraphID) REFERENCES Paragraphs(ParagraphID)
            ON DELETE CASCADE,
        CONSTRAINT UQ_ParagraphRuns_Para_Seq
            UNIQUE (ParagraphID, Seq),
        CONSTRAINT CK_ParagraphRuns_BreakHasNoText
            CHECK (IsBreak = 0 OR RunText IS NULL),
        CONSTRAINT CK_ParagraphRuns_EmphTypeOnlyWhenEmph
            CHECK (EmphType IS NULL OR IsEmph = 1)
    );

    CREATE INDEX IX_ParagraphRuns_Paragraph ON ParagraphRuns(ParagraphID, Seq);
END
GO

-- ------------------------------------------------------------
-- 2. Backfill: one run per existing paragraph
-- Guarded on the legacy column still existing (i.e. this hasn't
-- been renamed away yet) plus a per-row NOT EXISTS guard, so
-- this is safe against partial-failure re-runs.
-- ------------------------------------------------------------
IF COL_LENGTH('Paragraphs', 'ParagraphText') IS NOT NULL
BEGIN
    INSERT INTO ParagraphRuns (ParagraphID, Seq, RunText, IsEmph, EmphType, IsStrong, IsSmallCaps, OverlayType, IsBreak)
    SELECT p.ParagraphID, 1, p.ParagraphText, 0, NULL, 0, 0, NULL, 0
    FROM Paragraphs p
    WHERE NOT EXISTS (SELECT 1 FROM ParagraphRuns r WHERE r.ParagraphID = p.ParagraphID);
END
GO

-- ------------------------------------------------------------
-- 3. Rename legacy text column (rename, not drop)
-- ------------------------------------------------------------
IF COL_LENGTH('Paragraphs', 'ParagraphText') IS NOT NULL
    EXEC sp_rename 'Paragraphs.ParagraphText', 'ParagraphText_Legacy', 'COLUMN';
GO
