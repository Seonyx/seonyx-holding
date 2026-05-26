using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Seonyx.Web.Models;
using Seonyx.Web.Models.ViewModels.BookEditor;

namespace Seonyx.Web.Controllers
{
    [Authorize]
    public class CharacterController : Controller
    {
        private readonly SeonyxContext db = new SeonyxContext();

        // GET: /admin/bookeditor/characters/Index?bookProjectId=N
        public ActionResult Index(int bookProjectId)
        {
            var project = db.BookProjects.Find(bookProjectId);
            if (project == null) return HttpNotFound();

            var characters = db.Characters
                .Where(c => c.BookProjectID == bookProjectId)
                .Include(c => c.Tags)
                .OrderBy(c => c.Name)
                .ToList();

            var vm = new CharacterListViewModel
            {
                BookProjectID = bookProjectId,
                ProjectName   = project.ProjectName
            };

            foreach (var c in characters)
            {
                vm.Characters.Add(new CharacterSummaryViewModel
                {
                    CharacterId    = c.CharacterId,
                    Name           = c.Name,
                    StoryRoleCode  = c.StoryRoleCode,
                    ImportanceCode = c.ImportanceCode,
                    StatusCode     = c.StatusCode,
                    IsPov          = c.IsPov,
                    Tags           = c.Tags.Select(t => t.Tag).OrderBy(t => t).ToList()
                });
            }

            return View(vm);
        }

        // GET: /admin/bookeditor/characters/Edit?id=char_xxx&bookProjectId=N  (edit)
        // GET: /admin/bookeditor/characters/Edit?bookProjectId=N               (new)
        public ActionResult Edit(string id, int bookProjectId)
        {
            var project = db.BookProjects.Find(bookProjectId);
            if (project == null) return HttpNotFound();

            CharacterDetailViewModel vm;

            if (string.IsNullOrEmpty(id))
            {
                // New character
                vm = new CharacterDetailViewModel
                {
                    BookProjectID = bookProjectId,
                    ProjectName   = project.ProjectName
                };
            }
            else
            {
                var character = db.Characters
                    .Include(c => c.Aliases)
                    .Include(c => c.Tags)
                    .FirstOrDefault(c => c.CharacterId == id && c.BookProjectID == bookProjectId);

                if (character == null) return HttpNotFound();

                vm = MapToDetailViewModel(character, project.ProjectName);
            }

            return View(vm);
        }

        // POST: /admin/bookeditor/characters/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(CharacterDetailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ProjectName = db.BookProjects.Find(model.BookProjectID)?.ProjectName ?? "";
                return View("Edit", model);
            }

            // Server-side code validation
            if (!string.IsNullOrEmpty(model.StatusCode) &&
                !CharacterDetailViewModel.StatusCodes.Contains(model.StatusCode))
            {
                ModelState.AddModelError("StatusCode", "Invalid status code.");
            }
            if (!CharacterDetailViewModel.StoryRoleCodes.Contains(model.StoryRoleCode))
            {
                ModelState.AddModelError("StoryRoleCode", "Invalid story role code.");
            }
            if (!CharacterDetailViewModel.ImportanceCodes.Contains(model.ImportanceCode))
            {
                ModelState.AddModelError("ImportanceCode", "Invalid importance code.");
            }
            if (!string.IsNullOrEmpty(model.SpoilerLevelCode) &&
                !CharacterDetailViewModel.SpoilerLevelCodes.Contains(model.SpoilerLevelCode))
            {
                ModelState.AddModelError("SpoilerLevelCode", "Invalid spoiler level code.");
            }
            foreach (var alias in model.Aliases ?? new List<CharacterAliasViewModel>())
            {
                if (!string.IsNullOrEmpty(alias.AliasTypeCode) &&
                    !CharacterDetailViewModel.AliasTypeCodes.Contains(alias.AliasTypeCode))
                {
                    ModelState.AddModelError("Aliases", "Invalid alias type code: " + alias.AliasTypeCode);
                    break;
                }
            }

            if (!ModelState.IsValid)
            {
                model.ProjectName = db.BookProjects.Find(model.BookProjectID)?.ProjectName ?? "";
                return View("Edit", model);
            }

            // Parse tags from comma-separated input
            var newTags = ParseTags(model.TagsRaw);

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var existing = db.Characters
                        .Include(c => c.Aliases)
                        .Include(c => c.Tags)
                        .FirstOrDefault(c => c.CharacterId == model.CharacterId);

                    bool isNew = existing == null;

                    Character entity;
                    if (isNew)
                    {
                        entity = new Character
                        {
                            CharacterId   = model.CharacterId,
                            BookProjectID = model.BookProjectID,
                            CreatedAt     = DateTime.UtcNow
                        };
                        db.Characters.Add(entity);
                    }
                    else
                    {
                        entity = existing;

                        // Remove existing aliases and tags — delete-and-reinsert
                        db.CharacterAliases.RemoveRange(entity.Aliases.ToList());
                        db.CharacterTags.RemoveRange(entity.Tags.ToList());
                        db.SaveChanges();
                    }

                    MapFromDetailViewModel(model, entity);
                    entity.UpdatedAt = DateTime.UtcNow;

                    // Insert fresh aliases
                    foreach (var a in model.Aliases ?? new List<CharacterAliasViewModel>())
                    {
                        if (string.IsNullOrWhiteSpace(a.Alias)) continue;
                        db.CharacterAliases.Add(new CharacterAlias
                        {
                            CharacterAliasId = GenerateId("alias"),
                            CharacterId      = entity.CharacterId,
                            Alias            = a.Alias.Trim(),
                            AliasTypeCode    = string.IsNullOrEmpty(a.AliasTypeCode) ? null : a.AliasTypeCode,
                            SortOrder        = a.SortOrder,
                            Notes            = a.Notes
                        });
                    }

                    // Insert fresh tags
                    foreach (var tag in newTags)
                    {
                        db.CharacterTags.Add(new CharacterTag
                        {
                            CharacterTagId = GenerateId("tag"),
                            CharacterId    = entity.CharacterId,
                            Tag            = tag
                        });
                    }

                    db.SaveChanges();
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            TempData["Message"] = "Character saved.";
            return RedirectToAction("Index", new { bookProjectId = model.BookProjectID });
        }

        // POST: /admin/bookeditor/characters/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id, int bookProjectId)
        {
            var character = db.Characters.Find(id);
            if (character != null)
            {
                db.Characters.Remove(character);
                db.SaveChanges();
                TempData["Message"] = "Character deleted.";
            }
            return RedirectToAction("Index", new { bookProjectId });
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------

        private static CharacterDetailViewModel MapToDetailViewModel(Character c, string projectName)
        {
            return new CharacterDetailViewModel
            {
                CharacterId          = c.CharacterId,
                BookProjectID        = c.BookProjectID,
                ProjectName          = projectName,
                Name                 = c.Name,
                Pronouns             = c.Pronouns,
                AgeDisplay           = c.AgeDisplay,
                DateOfBirth          = c.DateOfBirth,
                SpeciesType          = c.SpeciesType,
                StatusCode           = c.StatusCode,
                StoryRoleCode        = c.StoryRoleCode,
                ImportanceCode       = c.ImportanceCode,
                SpoilerLevelCode     = c.SpoilerLevelCode,
                IsPov                = c.IsPov,
                HomeLocation         = c.HomeLocation,
                CurrentLocation      = c.CurrentLocation,
                Faction              = c.Faction,
                Occupation           = c.Occupation,
                ClassStatus          = c.ClassStatus,
                Goal                 = c.Goal,
                Need                 = c.Need,
                Motivation           = c.Motivation,
                Stakes               = c.Stakes,
                Fear                 = c.Fear,
                Flaw                 = c.Flaw,
                Strength             = c.Strength,
                LieTheyBelieve       = c.LieTheyBelieve,
                CoreValue            = c.CoreValue,
                InternalConflict     = c.InternalConflict,
                ExternalConflict     = c.ExternalConflict,
                Secret               = c.Secret,
                LineTheyWontCross    = c.LineTheyWontCross,
                PhysicalDescription  = c.PhysicalDescription,
                DistinctiveFeatures  = c.DistinctiveFeatures,
                ClothingProps        = c.ClothingProps,
                VoicePattern         = c.VoicePattern,
                HabitsMannerisms     = c.HabitsMannerisms,
                BodyLanguage         = c.BodyLanguage,
                PublicPersona        = c.PublicPersona,
                PrivateSelf          = c.PrivateSelf,
                EducationTraining    = c.EducationTraining,
                OriginBackground     = c.OriginBackground,
                ImportantPastEvents  = c.ImportantPastEvents,
                FamilyNotes          = c.FamilyNotes,
                WeaknessesLimitations = c.WeaknessesLimitations,
                HealthInjuries       = c.HealthInjuries,
                StoryArcSummary      = c.StoryArcSummary,
                CharacterFunction    = c.CharacterFunction,
                RelationshipSummary  = c.RelationshipSummary,
                ContinuityNotes      = c.ContinuityNotes,
                UnresolvedThreads    = c.UnresolvedThreads,
                Notes                = c.Notes,
                ReferenceImageUri    = c.ReferenceImageUri,
                Aliases              = c.Aliases
                    .OrderBy(a => a.SortOrder ?? 99)
                    .ThenBy(a => a.Alias)
                    .Select(a => new CharacterAliasViewModel
                    {
                        CharacterAliasId = a.CharacterAliasId,
                        Alias            = a.Alias,
                        AliasTypeCode    = a.AliasTypeCode,
                        SortOrder        = a.SortOrder,
                        Notes            = a.Notes
                    }).ToList(),
                TagsRaw = string.Join(", ", c.Tags.OrderBy(t => t.Tag).Select(t => t.Tag))
            };
        }

        private static void MapFromDetailViewModel(CharacterDetailViewModel vm, Character c)
        {
            c.Name                 = vm.Name;
            c.Pronouns             = vm.Pronouns;
            c.AgeDisplay           = vm.AgeDisplay;
            c.DateOfBirth          = vm.DateOfBirth;
            c.SpeciesType          = vm.SpeciesType;
            c.StatusCode           = string.IsNullOrEmpty(vm.StatusCode) ? null : vm.StatusCode;
            c.StoryRoleCode        = vm.StoryRoleCode;
            c.ImportanceCode       = vm.ImportanceCode;
            c.SpoilerLevelCode     = string.IsNullOrEmpty(vm.SpoilerLevelCode) ? null : vm.SpoilerLevelCode;
            c.IsPov                = vm.IsPov;
            c.HomeLocation         = vm.HomeLocation;
            c.CurrentLocation      = vm.CurrentLocation;
            c.Faction              = vm.Faction;
            c.Occupation           = vm.Occupation;
            c.ClassStatus          = vm.ClassStatus;
            c.Goal                 = vm.Goal;
            c.Need                 = vm.Need;
            c.Motivation           = vm.Motivation;
            c.Stakes               = vm.Stakes;
            c.Fear                 = vm.Fear;
            c.Flaw                 = vm.Flaw;
            c.Strength             = vm.Strength;
            c.LieTheyBelieve       = vm.LieTheyBelieve;
            c.CoreValue            = vm.CoreValue;
            c.InternalConflict     = vm.InternalConflict;
            c.ExternalConflict     = vm.ExternalConflict;
            c.Secret               = vm.Secret;
            c.LineTheyWontCross    = vm.LineTheyWontCross;
            c.PhysicalDescription  = vm.PhysicalDescription;
            c.DistinctiveFeatures  = vm.DistinctiveFeatures;
            c.ClothingProps        = vm.ClothingProps;
            c.VoicePattern         = vm.VoicePattern;
            c.HabitsMannerisms     = vm.HabitsMannerisms;
            c.BodyLanguage         = vm.BodyLanguage;
            c.PublicPersona        = vm.PublicPersona;
            c.PrivateSelf          = vm.PrivateSelf;
            c.EducationTraining    = vm.EducationTraining;
            c.OriginBackground     = vm.OriginBackground;
            c.ImportantPastEvents  = vm.ImportantPastEvents;
            c.FamilyNotes          = vm.FamilyNotes;
            c.WeaknessesLimitations = vm.WeaknessesLimitations;
            c.HealthInjuries       = vm.HealthInjuries;
            c.StoryArcSummary      = vm.StoryArcSummary;
            c.CharacterFunction    = vm.CharacterFunction;
            c.RelationshipSummary  = vm.RelationshipSummary;
            c.ContinuityNotes      = vm.ContinuityNotes;
            c.UnresolvedThreads    = vm.UnresolvedThreads;
            c.Notes                = vm.Notes;
            c.ReferenceImageUri    = vm.ReferenceImageUri;
        }

        private static List<string> ParseTags(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            return raw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList();
        }

        private static readonly Random Rng = new Random();

        private static string GenerateId(string prefix)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var token = new char[6];
            lock (Rng)
            {
                for (int i = 0; i < token.Length; i++)
                    token[i] = chars[Rng.Next(chars.Length)];
            }
            return prefix + "_" + new string(token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
