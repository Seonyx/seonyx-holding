-- Migration: rename Character/CharacterAlias/CharacterTag tables to plural names
-- Run this if you already ran CharacterSketch_DDL.sql against the Seonyx database.
-- Safe to run only once.

-- Drop old FKs that reference [Character] before renaming
ALTER TABLE CharacterAlias DROP CONSTRAINT FK_CharAlias_Character;
ALTER TABLE CharacterTag   DROP CONSTRAINT FK_CharTag_Character;

-- Drop old PK on [Character] (rename will carry it, but we need to re-establish
-- the FKs from the renamed child tables)
-- Note: sp_rename renames the table object; constraints remain with the table.

EXEC sp_rename 'Character',     'Characters';
EXEC sp_rename 'CharacterAlias','CharacterAliases';
EXEC sp_rename 'CharacterTag',  'CharacterTags';

-- Re-add the FKs now pointing at renamed parent
ALTER TABLE CharacterAliases
    ADD CONSTRAINT FK_CharAliases_Characters
    FOREIGN KEY (CharacterId) REFERENCES Characters (CharacterId) ON DELETE CASCADE;

ALTER TABLE CharacterTags
    ADD CONSTRAINT FK_CharTags_Characters
    FOREIGN KEY (CharacterId) REFERENCES Characters (CharacterId) ON DELETE CASCADE;
