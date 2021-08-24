using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace traktorImpo
{
    public class TraktorSqlContext : DbContext
    {
        public DbSet<Post> wp_posts { get; set; }
        public DbSet<PostMeta> wp_postmeta { get; set; }
        public DbSet<TermRelation> wp_term_relationships { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //var connectionString = "server=localhost;database=traktora_traktoram_db;user=traktoram;password=aYLD7YupbsQx;convert zero datetime=True";
            var connectionString = "server=159.89.99.195;database=traktoram_1625579110;user=sync;password=AA9KH7ECvFENKRDD;convert zero datetime=True";
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
