using System.Data.Entity;

namespace Seonyx.Web.Models
{
    public class SeonyxContext : DbContext
    {
        public SeonyxContext() : base("SeonyxContext")
        {
            Database.SetInitializer<SeonyxContext>(null);
        }

        public DbSet<Page> Pages { get; set; }
        public DbSet<Division> Divisions { get; set; }
        public DbSet<ContentBlock> ContentBlocks { get; set; }
        public DbSet<ContactSubmission> ContactSubmissions { get; set; }
        public DbSet<PaidContactSubmission> PaidContactSubmissions { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }

        // Book Editor tables
        public DbSet<BookProject> BookProjects { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Paragraph> Paragraphs { get; set; }
        public DbSet<MetaNote> MetaNotes { get; set; }
        public DbSet<EditNote> EditNotes { get; set; }

        // BookML versioning tables
        public DbSet<Draft> Drafts { get; set; }
        public DbSet<ParagraphVersion> ParagraphVersions { get; set; }
        public DbSet<ImportLog> ImportLogs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Page>()
                .HasOptional(p => p.ParentPage)
                .WithMany(p => p.ChildPages)
                .HasForeignKey(p => p.ParentPageId);

            modelBuilder.Entity<Page>()
                .HasOptional(p => p.Division)
                .WithMany(d => d.Pages)
                .HasForeignKey(p => p.DivisionId);

            modelBuilder.Entity<Book>()
                .HasRequired(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId);

            // Book Editor relationships
            modelBuilder.Entity<Chapter>()
                .HasRequired(c => c.BookProject)
                .WithMany(b => b.Chapters)
                .HasForeignKey(c => c.BookProjectID)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Paragraph>()
                .HasRequired(p => p.Chapter)
                .WithMany(c => c.Paragraphs)
                .HasForeignKey(p => p.ChapterID)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<MetaNote>()
                .HasRequired(m => m.Paragraph)
                .WithMany()
                .HasForeignKey(m => m.ParagraphID)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<EditNote>()
                .HasRequired(e => e.Paragraph)
                .WithMany()
                .HasForeignKey(e => e.ParagraphID)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Draft>()
                .HasRequired(d => d.BookProject)
                .WithMany()
                .HasForeignKey(d => d.BookProjectID)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ParagraphVersion>()
                .HasRequired(v => v.Chapter)
                .WithMany()
                .HasForeignKey(v => v.ChapterID)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ImportLog>()
                .HasRequired(l => l.BookProject)
                .WithMany()
                .HasForeignKey(l => l.BookProjectID)
                .WillCascadeOnDelete(false);
        }
    }
}
