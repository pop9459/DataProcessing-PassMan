using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public enum AuditAction
    {
        // User actions
        UserRegistered = 100,
        UserLoggedIn = 101,
        UserLoggedOut = 102,
        UserPasswordChanged = 103,
        UserRoleChanged = 104,

        // Vault actions
        VaultCreated = 200,
        VaultUpdated = 201,
        VaultDeleted = 202,
        VaultShared = 203,
        VaultShareRevoked = 204,

        // Credential actions
        CredentialCreated = 300,
        CredentialUpdated = 301,
        CredentialDeleted = 302,
        CredentialViewed = 303,

        // Security events
        FailedLoginAttempt = 400,
        SuspiciousActivity = 401,
    }

    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public AuditAction Action { get; set; }

        [MaxLength(50)]
        public string? EntityType { get; set; } // "Credential", "Vault", "User"

        public int? EntityId { get; set; }

        [Column(TypeName = "TEXT")]
        public string? Details { get; set; }

        [Required]
        public int UserId { get; set; }

        // Direct links to Vault and Credential (co-exist with EntityType/EntityId pattern)
        public int? VaultId { get; set; }
        public int? CredentialId { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("VaultId")]
        public virtual Vault? Vault { get; set; }

        [ForeignKey("CredentialId")]
        public virtual Credential? Credential { get; set; }
    }
}
