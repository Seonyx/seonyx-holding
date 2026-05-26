using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Characters")]
    public class Character
    {
        [Key]
        [StringLength(40)]
        public string CharacterId { get; set; }

        public int BookProjectID { get; set; }

        // Identity
        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(100)]
        public string Pronouns { get; set; }

        [StringLength(50)]
        public string AgeDisplay { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(100)]
        public string SpeciesType { get; set; }

        // Classification
        [StringLength(40)]
        public string StatusCode { get; set; }

        [Required]
        [StringLength(40)]
        public string StoryRoleCode { get; set; }

        [Required]
        [StringLength(40)]
        public string ImportanceCode { get; set; }

        [StringLength(40)]
        public string SpoilerLevelCode { get; set; }

        public bool IsPov { get; set; }

        // Location & faction
        [StringLength(200)]
        public string HomeLocation { get; set; }

        [StringLength(200)]
        public string CurrentLocation { get; set; }

        [StringLength(200)]
        public string Faction { get; set; }

        // Occupation
        [StringLength(200)]
        public string Occupation { get; set; }

        [StringLength(200)]
        public string ClassStatus { get; set; }

        // Psychology
        public string Goal { get; set; }
        public string Need { get; set; }
        public string Motivation { get; set; }
        public string Stakes { get; set; }
        public string Fear { get; set; }
        public string Flaw { get; set; }
        public string Strength { get; set; }
        public string LieTheyBelieve { get; set; }
        public string CoreValue { get; set; }
        public string InternalConflict { get; set; }
        public string ExternalConflict { get; set; }
        public string Secret { get; set; }
        public string LineTheyWontCross { get; set; }

        // Appearance & behaviour
        public string PhysicalDescription { get; set; }
        public string DistinctiveFeatures { get; set; }
        public string ClothingProps { get; set; }
        public string VoicePattern { get; set; }
        public string HabitsMannerisms { get; set; }
        public string BodyLanguage { get; set; }
        public string PublicPersona { get; set; }
        public string PrivateSelf { get; set; }

        // Background
        public string EducationTraining { get; set; }
        public string OriginBackground { get; set; }
        public string ImportantPastEvents { get; set; }
        public string FamilyNotes { get; set; }
        public string WeaknessesLimitations { get; set; }
        public string HealthInjuries { get; set; }

        // Story-level notes
        public string StoryArcSummary { get; set; }
        public string CharacterFunction { get; set; }
        public string RelationshipSummary { get; set; }
        public string ContinuityNotes { get; set; }
        public string UnresolvedThreads { get; set; }
        public string Notes { get; set; }

        [StringLength(500)]
        public string ReferenceImageUri { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public virtual BookProject BookProject { get; set; }
        public virtual ICollection<CharacterAlias> Aliases { get; set; }
        public virtual ICollection<CharacterTag> Tags { get; set; }

        public Character()
        {
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            IsPov = false;
            Aliases = new HashSet<CharacterAlias>();
            Tags = new HashSet<CharacterTag>();
        }
    }
}
