using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PassManAPI.Models
{
    /// <summary>
    /// User entity that extends IdentityUser for authentication
    /// Uses built-in UserName property from IdentityUser for display name
    /// </summary>
    public class User : IdentityUser<int>
    {
        /// <summary>
        /// When the user account was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last time the user logged in
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Encrypted master key for this user's vaults (for future encryption implementation)
        /// </summary>
        public string? EncryptedVaultKey { get; set; }

        // Navigation properties (will be added when we create Vault model)
        // public ICollection<Vault> Vaults { get; set; } = new List<Vault>();
    }
}