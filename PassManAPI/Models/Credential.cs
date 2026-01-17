using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public class Credential
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Username { get; set; }

        [Required]
        public string EncryptedPassword { get; set; } = string.Empty;

        [MaxLength(500)]
        [Url]
        public string? Url { get; set; }

        [Column(TypeName = "TEXT")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastAccessed { get; set; }

        // Foreign keys
        [Required]
        public int VaultId { get; set; }

        public int? CategoryId { get; set; }

        // Navigation properties
        [ForeignKey("VaultId")]
        public virtual Vault Vault { get; set; } = null!;

        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
