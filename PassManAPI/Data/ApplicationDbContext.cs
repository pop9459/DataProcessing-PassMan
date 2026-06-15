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
        // Core DbSets
        public DbSet<Vault> Vaults { get; set; }
        public DbSet<Credential> Credentials { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<VaultShare> VaultShares { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Invitation> Invitations { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<CredentialTag> CredentialTags { get; set; }

        /// <summary>
        /// Keyless DbSet backed by the vwUserVaultAccess view.
        /// Use this to check or list vault access without duplicating the Owner/Shared union logic.
        /// </summary>
        public DbSet<VaultAccessRow> VaultAccess { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Helper to get correct timestamp SQL based on provider
            var timestampSql = Database.IsMySql() ? "CURRENT_TIMESTAMP(6)" : "CURRENT_TIMESTAMP";

            // User configurations
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder
                .Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql(timestampSql);

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
                .HasDefaultValueSql(timestampSql);

            // Global query filter for soft delete - automatically excludes deleted vaults
            modelBuilder
                .Entity<Vault>()
                .HasQueryFilter(v => !v.IsDeleted);

            // Propagate the vault soft-delete filter to entities that belong to a vault. Without
            // this, only Vault queries honor IsDeleted: a soft-deleted vault's credentials and
            // shares stay queryable, so users the vault was shared with keep full access to its
            // credentials after the owner "deleted" it. Audit history bypasses these filters via
            // IgnoreQueryFilters where it deliberately needs deleted vaults.
            modelBuilder.Entity<Credential>().HasQueryFilter(c => !c.Vault.IsDeleted);
            modelBuilder.Entity<VaultShare>().HasQueryFilter(vs => !vs.Vault.IsDeleted);

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
                .HasDefaultValueSql(timestampSql);

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
                .HasOne(al => al.Vault)
                .WithMany(v => v.AuditLogs)
                .HasForeignKey(al => al.VaultId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder
                .Entity<AuditLog>()
                .HasOne(al => al.Credential)
                .WithMany(c => c.AuditLogs)
                .HasForeignKey(al => al.CredentialId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder
                .Entity<AuditLog>()
                .Property(al => al.Timestamp)
                .HasDefaultValueSql(timestampSql);

            // Attachment configurations
            modelBuilder
                .Entity<Attachment>()
                .HasOne(a => a.Credential)
                .WithMany(c => c.Attachments)
                .HasForeignKey(a => a.CredentialId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Attachment>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql(timestampSql);

            // Invitation configurations
            modelBuilder
                .Entity<Invitation>()
                .HasOne(i => i.Vault)
                .WithMany(v => v.Invitations)
                .HasForeignKey(i => i.VaultId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Invitation>()
                .HasIndex(i => i.InviteToken)
                .IsUnique();

            modelBuilder
                .Entity<Invitation>()
                .HasIndex(i => new { i.VaultId, i.InvitedEmail });

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

            // Keyless entity mapped to the vwUserVaultAccess view (MySQL only).
            // HasNoKey() tells EF Core this is read-only and has no primary key.
            modelBuilder.Entity<VaultAccessRow>()
                .HasNoKey()
                .ToView("vwUserVaultAccess");

        }
    }
}
