using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    /// <summary>
    /// Defines the feature limits and pricing for each subscription level.
    /// </summary>
    public class SubscriptionTier
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public int MaxVaults { get; set; } = 3;

        public int MaxCredentialsPerVault { get; set; } = 50;

        public int MaxAttachmentSizeMB { get; set; } = 5;

        public bool AllowsSharing { get; set; } = false;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; } = 0;

        // Default tiers for seed data
        public static readonly List<SubscriptionTier> DefaultTiers = new()
        {
            new SubscriptionTier
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Name = "Free",
                MaxVaults = 3,
                MaxCredentialsPerVault = 50,
                MaxAttachmentSizeMB = 5,
                AllowsSharing = false,
                Price = 0
            },
            new SubscriptionTier
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Name = "Premium",
                MaxVaults = 25,
                MaxCredentialsPerVault = 500,
                MaxAttachmentSizeMB = 50,
                AllowsSharing = true,
                Price = 4.99m
            }
        };

        // Navigation
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
