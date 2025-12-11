using Microsoft.EntityFrameworkCore;
using PassManAPI.Models;

namespace PassManAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // DbSets will go here (we'll add them after creating models)
        // public DbSet<User> Users { get; set; }
        // public DbSet<Vault> Vaults { get; set; }
        // public DbSet<Credential> Credentials { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // We'll configure relationships here later
        }
    }
}
