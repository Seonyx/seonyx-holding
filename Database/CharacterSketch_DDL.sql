-- Character Sketch module -- Phase 1
-- Run against the Seonyx database on Dibbler after copying files.
-- Tables: Characters, CharacterAliases, CharacterTags

CREATE TABLE Characters (
    CharacterId              VARCHAR(40)   NOT NULL,
    BookProjectID            INT           NOT NULL,

    -- Identity
    Name                     NVARCHAR(200) NOT NULL,
    Pronouns                 NVARCHAR(100) NULL,
    AgeDisplay               NVARCHAR(50)  NULL,
    DateOfBirth              DATE          NULL,
    SpeciesType              NVARCHAR(100) NULL,

    -- Classification (app-validated codes)
    StatusCode               VARCHAR(40)   NULL,
    StoryRoleCode            VARCHAR(40)   NOT NULL,
    ImportanceCode           VARCHAR(40)   NOT NULL,
    SpoilerLevelCode         VARCHAR(40)   NULL,
    IsPov                    BIT           NOT NULL DEFAULT 0,

    -- Location & faction
    HomeLocation             NVARCHAR(200) NULL,
    CurrentLocation          NVARCHAR(200) NULL,
    Faction                  NVARCHAR(200) NULL,

    -- Occupation
    Occupation               NVARCHAR(200) NULL,
    ClassStatus              NVARCHAR(200) NULL,

    -- Psychology
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

    CONSTRAINT PK_Characters PRIMARY KEY (CharacterId),
    CONSTRAINT FK_Characters_BookProject FOREIGN KEY (BookProjectID)
        REFERENCES BookProjects (BookProjectID)
);

CREATE INDEX IX_Characters_BookProjectID ON Characters (BookProjectID);

-- -------------------------------------------------------

CREATE TABLE CharacterAliases (
    CharacterAliasId         VARCHAR(40)   NOT NULL,
    CharacterId              VARCHAR(40)   NOT NULL,
    Alias                    NVARCHAR(200) NOT NULL,
    AliasTypeCode            VARCHAR(40)   NULL,
    SortOrder                INT           NULL,
    Notes                    NVARCHAR(MAX) NULL,

    CONSTRAINT PK_CharacterAliases PRIMARY KEY (CharacterAliasId),
    CONSTRAINT FK_CharAliases_Characters FOREIGN KEY (CharacterId)
        REFERENCES Characters (CharacterId) ON DELETE CASCADE
);

CREATE INDEX IX_CharacterAliases_CharacterId ON CharacterAliases (CharacterId);

-- -------------------------------------------------------

CREATE TABLE CharacterTags (
    CharacterTagId           VARCHAR(40)   NOT NULL,
    CharacterId              VARCHAR(40)   NOT NULL,
    Tag                      NVARCHAR(100) NOT NULL,

    CONSTRAINT PK_CharacterTags PRIMARY KEY (CharacterTagId),
    CONSTRAINT FK_CharTags_Characters FOREIGN KEY (CharacterId)
        REFERENCES Characters (CharacterId) ON DELETE CASCADE
);

CREATE UNIQUE INDEX UX_CharacterTags_CharTag ON CharacterTags (CharacterId, Tag);
