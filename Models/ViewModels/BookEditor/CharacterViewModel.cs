using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Seonyx.Web.Models.ViewModels.BookEditor
{
    // -------------------------------------------------------
    // List page
    // -------------------------------------------------------

    public class CharacterListViewModel
    {
        public int    BookProjectID { get; set; }
        public string ProjectName  { get; set; }
        public List<CharacterSummaryViewModel> Characters { get; set; }

        public CharacterListViewModel()
        {
            Characters = new List<CharacterSummaryViewModel>();
        }
    }

    public class CharacterSummaryViewModel
    {
        public string CharacterId    { get; set; }
        public string Name           { get; set; }
        public string StoryRoleCode  { get; set; }
        public string ImportanceCode { get; set; }
        public string StatusCode     { get; set; }
        public bool   IsPov          { get; set; }
        public List<string> Tags     { get; set; }

        public CharacterSummaryViewModel()
        {
            Tags = new List<string>();
        }
    }

    // -------------------------------------------------------
    // Edit / detail page
    // -------------------------------------------------------

    public class CharacterDetailViewModel
    {
        // Key
        public string CharacterId    { get; set; }
        public int    BookProjectID  { get; set; }
        public string ProjectName    { get; set; }

        // Identity
        [Required]
        [StringLength(200)]
        public string Name           { get; set; }

        [StringLength(100)]
        public string Pronouns       { get; set; }

        [StringLength(50)]
        public string AgeDisplay     { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(100)]
        public string SpeciesType    { get; set; }

        // Classification
        [StringLength(40)]
        public string StatusCode     { get; set; }

        [Required]
        [StringLength(40)]
        public string StoryRoleCode  { get; set; }

        [Required]
        [StringLength(40)]
        public string ImportanceCode { get; set; }

        [StringLength(40)]
        public string SpoilerLevelCode { get; set; }

        public bool IsPov            { get; set; }

        // Location & faction
        [StringLength(200)]
        public string HomeLocation   { get; set; }

        [StringLength(200)]
        public string CurrentLocation { get; set; }

        [StringLength(200)]
        public string Faction        { get; set; }

        // Occupation
        [StringLength(200)]
        public string Occupation     { get; set; }

        [StringLength(200)]
        public string ClassStatus    { get; set; }

        // Psychology
        public string Goal              { get; set; }
        public string Need              { get; set; }
        public string Motivation        { get; set; }
        public string Stakes            { get; set; }
        public string Fear              { get; set; }
        public string Flaw              { get; set; }
        public string Strength          { get; set; }
        public string LieTheyBelieve    { get; set; }
        public string CoreValue         { get; set; }
        public string InternalConflict  { get; set; }
        public string ExternalConflict  { get; set; }
        public string Secret            { get; set; }
        public string LineTheyWontCross { get; set; }

        // Appearance & behaviour
        public string PhysicalDescription  { get; set; }
        public string DistinctiveFeatures  { get; set; }
        public string ClothingProps        { get; set; }
        public string VoicePattern         { get; set; }
        public string HabitsMannerisms     { get; set; }
        public string BodyLanguage         { get; set; }
        public string PublicPersona        { get; set; }
        public string PrivateSelf          { get; set; }

        // Background
        public string EducationTraining    { get; set; }
        public string OriginBackground     { get; set; }
        public string ImportantPastEvents  { get; set; }
        public string FamilyNotes          { get; set; }
        public string WeaknessesLimitations { get; set; }
        public string HealthInjuries       { get; set; }

        // Story-level notes
        public string StoryArcSummary      { get; set; }
        public string CharacterFunction    { get; set; }
        public string RelationshipSummary  { get; set; }
        public string ContinuityNotes      { get; set; }
        public string UnresolvedThreads    { get; set; }
        public string Notes                { get; set; }

        [StringLength(500)]
        public string ReferenceImageUri    { get; set; }

        // Aliases (editable rows)
        public List<CharacterAliasViewModel> Aliases { get; set; }

        // Tags (comma-separated on form; split server-side)
        public string TagsRaw { get; set; }

        // -------------------------------------------------------
        // Static code lists for dropdowns
        // -------------------------------------------------------

        public static readonly string[] StatusCodes =
            { "alive", "dead", "missing", "unknown", "inactive" };

        public static readonly string[] StoryRoleCodes =
            { "protagonist", "antagonist", "deuteragonist", "ally", "mentor",
              "foil", "rival", "authority", "supporting", "background" };

        public static readonly string[] ImportanceCodes =
            { "major", "secondary", "minor", "mentioned_only" };

        public static readonly string[] SpoilerLevelCodes =
            { "none", "mild", "moderate", "major", "full" };

        public static readonly string[] AliasTypeCodes =
            { "nickname", "title", "codename", "birth_name", "public_name" };

        public CharacterDetailViewModel()
        {
            Aliases = new List<CharacterAliasViewModel>();
        }
    }

    public class CharacterAliasViewModel
    {
        public string CharacterAliasId { get; set; }
        public string Alias            { get; set; }
        public string AliasTypeCode    { get; set; }
        public int?   SortOrder        { get; set; }
        public string Notes            { get; set; }
    }
}
