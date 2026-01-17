using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PassManAPI.Models
{
    /// <summary>
    /// Identity-backed user with additional metadata for vault ownership/audit.
    /// </summary>
    [Index(nameof(Email), IsUnique = true)]
    public class User : IdentityUser<int>
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? EncryptedVaultKey { get; set; }

        /// <summary>
        /// Base32-encoded TOTP secret for 2FA authentication.
        /// </summary>
        public string? TotpSecret { get; set; }

        /// <summary>
        /// Link to the user's subscription tier.
        /// </summary>
        public Guid? SubscriptionTierId { get; set; }

        // Navigation properties
        public virtual ICollection<Vault> Vaults { get; set; } = new List<Vault>();
        public virtual ICollection<VaultShare> SharedVaults { get; set; } = new List<VaultShare>();
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
