using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Models;

namespace PassManAPI.Data
{
    /// <summary>
    /// Application database context with Identity integration
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // DbSets for our custom entities (will add as we create models)
        // public DbSet<Vault> Vaults { get; set; }
        // public DbSet<VaultItem> VaultItems { get; set; }
        // public DbSet<VaultShare> VaultShares { get; set; }
        // public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.CreatedAt)
                    .IsRequired();
            });

            // Customize Identity table names (optional - makes tables cleaner)
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<IdentityRole<int>>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");
            modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
        }
    }
}
