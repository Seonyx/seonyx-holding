-- Add Author column to BookProjects table
-- Run once against the Seonyx database

ALTER TABLE BookProjects
    ADD Author NVARCHAR(255) NULL;
