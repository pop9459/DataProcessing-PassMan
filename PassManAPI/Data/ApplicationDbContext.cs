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
        public DbSet<Tag> Tags { get; set; }
        public DbSet<CredentialTag> CredentialTags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder
                .Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql(Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? "CURRENT_TIMESTAMP" : "CURRENT_TIMESTAMP(6)");

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
                .HasDefaultValueSql(Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? "CURRENT_TIMESTAMP" : "CURRENT_TIMESTAMP(6)");

            // Global query filter for soft delete - automatically excludes deleted vaults
            modelBuilder
                .Entity<Vault>()
                .HasQueryFilter(v => !v.IsDeleted);

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
                .HasDefaultValueSql(Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? "CURRENT_TIMESTAMP" : "CURRENT_TIMESTAMP(6)");

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
                .HasDefaultValueSql(Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? "CURRENT_TIMESTAMP" : "CURRENT_TIMESTAMP(6)");

            // Tag configurations
            modelBuilder
                .Entity<Tag>()
                .HasIndex(t => t.Name)
                .IsUnique();

            // CredentialTag configurations (composite key for many-to-many)
            modelBuilder.Entity<CredentialTag>().HasKey(ct => new { ct.CredentialId, ct.TagId });

            modelBuilder
                .Entity<CredentialTag>()
                .HasOne(ct => ct.Credential)
                .WithMany(c => c.CredentialTags)
                .HasForeignKey(ct => ct.CredentialId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<CredentialTag>()
                .HasOne(ct => ct.Tag)
                .WithMany(t => t.CredentialTags)
                .HasForeignKey(ct => ct.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed default categories
            modelBuilder.Entity<Category>().HasData(Category.DefaultCategories);
        }
    }
}
