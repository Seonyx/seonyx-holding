-- Add SourceFileName column to ImportLogs
-- Run once against the Seonyx database.
-- Safe to re-run: the column is added only if it does not already exist.

IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.ImportLogs')
    AND    name      = N'SourceFileName'
)
BEGIN
    ALTER TABLE dbo.ImportLogs
        ADD SourceFileName NVARCHAR(255) NULL;

    PRINT 'Column ImportLogs.SourceFileName added.';
END
ELSE
BEGIN
    PRINT 'Column ImportLogs.SourceFileName already exists — skipped.';
END
