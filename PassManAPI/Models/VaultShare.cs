using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public enum SharePermission
    {
        View = 0, // Can only view credentials
        Edit = 1, // Can view and edit credentials
        Admin = 2, // Can view, edit, and manage sharing
    }

    public class VaultShare
    {
        // Composite primary key
        [Key, Column(Order = 0)]
        public int VaultId { get; set; }

        [Key, Column(Order = 1)]
        public int UserId { get; set; }

        [Required]
        public SharePermission Permission { get; set; } = SharePermission.View;

        public DateTime SharedAt { get; set; } = DateTime.UtcNow;

        public int? SharedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("VaultId")]
        public virtual Vault Vault { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("SharedByUserId")]
        public virtual User? SharedByUser { get; set; }
    }
}
