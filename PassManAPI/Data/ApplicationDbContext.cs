using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Models;

namespace PassManAPI.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Core DbSets
        public DbSet<Vault> Vaults { get; set; }
        public DbSet<Credential> Credentials { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<VaultShare> VaultShares { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder
                .Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Vault configurations
            modelBuilder
                .Entity<Vault>()
                .HasOne(v => v.User)
                .WithMany(u => u.Vaults)
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Vault>()
                .Property(v => v.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Credential configurations
            modelBuilder
                .Entity<Credential>()
                .HasOne(c => c.Vault)
                .WithMany(v => v.Credentials)
                .HasForeignKey(c => c.VaultId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Credential>()
                .HasOne(c => c.Category)
                .WithMany(cat => cat.Credentials)
                .HasForeignKey(c => c.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder
                .Entity<Credential>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // VaultShare configurations (composite key)
            modelBuilder.Entity<VaultShare>().HasKey(vs => new { vs.VaultId, vs.UserId });

            modelBuilder
                .Entity<VaultShare>()
                .HasOne(vs => vs.Vault)
                .WithMany(v => v.SharedUsers)
                .HasForeignKey(vs => vs.VaultId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<VaultShare>()
                .HasOne(vs => vs.User)
                .WithMany(u => u.SharedVaults)
                .HasForeignKey(vs => vs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // AuditLog configurations
            modelBuilder
                .Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<AuditLog>()
                .Property(al => al.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Seed default categories
            modelBuilder.Entity<Category>().HasData(Category.DefaultCategories);
        }
    }
}
