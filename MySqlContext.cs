using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace traktorImpo
{
    public class TraktorSqlContext : DbContext
    {
        private readonly string user;
        private readonly string password;
        private readonly string server;
        private readonly string dbName;

        public TraktorSqlContext(string user, string password, string server, string dbName) : base()
        {
            this.user = user;
            this.password = password;
            this.server = server;
            this.dbName = dbName;
        }

        public DbSet<Post> wp_posts { get; set; }
        public DbSet<PostMeta> wp_postmeta { get; set; }
        public DbSet<TermRelation> wp_term_relationships { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = string.Format("server={0};database={1};user={2};password={3};convert zero datetime=True", server, dbName, user, password);
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TermRelation>().HasKey(vf => new { vf.object_id, vf.term_taxonomy_id });
        }

        public void SaveWithRetry()
        {
            bool saveFailed;
            do
            {
                saveFailed = false;

                try
                {
                    SaveChanges();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    saveFailed = true;

                    // Update the values of the entity that failed to save from the store
                    ex.Entries.Single().Reload();
                }

            } while (saveFailed);
        }
    }
}
