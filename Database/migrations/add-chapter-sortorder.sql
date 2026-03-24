-- Migration: add SortOrder column to Chapters
-- Decouples display number (ChapterNumber) from sort position so that
-- unnumbered components (epilogue, prologue, etc.) can be ordered correctly.
--
-- Run once against the Seonyx database.

ALTER TABLE Chapters
    ADD SortOrder INT NOT NULL DEFAULT 0;
GO

-- Seed existing rows: numbered chapters use their ChapterNumber as sort key.
-- Unnumbered rows (ChapterNumber = 0) stay at 0 for now and will be
-- corrected on next import.
UPDATE Chapters SET SortOrder = ChapterNumber WHERE ChapterNumber > 0;
GO
