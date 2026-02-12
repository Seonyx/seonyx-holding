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
        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }

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
        }
    }
}
