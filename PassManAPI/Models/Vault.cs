using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public class Vault
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Optional icon identifier (emoji or icon name) for the vault.
        /// </summary>
        [MaxLength(50)]
        public string? Icon { get; set; }

        /// <summary>
        /// Soft delete flag. When true, the vault is considered deleted but retained in DB.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Foreign key to User (owner)
        [Required]
        public int UserId { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<Credential> Credentials { get; set; } = new List<Credential>();
        public virtual ICollection<VaultShare> SharedUsers { get; set; } = new List<VaultShare>();

        // TODO: Add Invitations navigation property when Invitation model is implemented
        // public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    }
}
