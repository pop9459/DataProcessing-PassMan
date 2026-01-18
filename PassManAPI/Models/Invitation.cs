using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public enum AccessRole
    {
        View = 0,   // Can only view credentials
        Edit = 1,   // Can view and edit credentials
        Admin = 2   // Can view, edit, and manage sharing
    }

    public class Invitation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int VaultId { get; set; }

        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string InvitedEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string InviteToken { get; set; } = string.Empty;

        [Required]
        public AccessRole Role { get; set; } = AccessRole.View;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime? AcceptedAt { get; set; }

        // Navigation property
        [ForeignKey("VaultId")]
        public virtual Vault Vault { get; set; } = null!;

        // Constructor
        public Invitation(int vaultId, string invitedEmail, AccessRole role, DateTime expiresAt, string inviteToken)
        {
            if (string.IsNullOrWhiteSpace(invitedEmail))
            {
                throw new ArgumentException("Invited email cannot be null or whitespace.", nameof(invitedEmail));
            }

            if (!new EmailAddressAttribute().IsValid(invitedEmail))
            {
                throw new ArgumentException("Invalid email address format.", nameof(invitedEmail));
            }

            if (string.IsNullOrWhiteSpace(inviteToken))
            {
                throw new ArgumentException("Invite token cannot be null or whitespace.", nameof(inviteToken));
            }

            if (inviteToken.Length > 100)
            {
                throw new ArgumentException("Invite token cannot exceed 100 characters.", nameof(inviteToken));
            }

            if (expiresAt <= DateTime.UtcNow)
            {
                throw new ArgumentException("Expiration date must be in the future.", nameof(expiresAt));
            }

            VaultId = vaultId;
            InvitedEmail = invitedEmail;
            Role = role;
            ExpiresAt = expiresAt;
            InviteToken = inviteToken;
            AcceptedAt = null;
        }

        // Default constructor for EF Core
        public Invitation() { }

        // Operations (Methods)
        public void MarkAccepted()
        {
            if (AcceptedAt.HasValue)
            {
                throw new InvalidOperationException("Invitation has already been accepted.");
            }

            if (IsExpired())
            {
                throw new InvalidOperationException("Cannot accept an expired invitation.");
            }

            AcceptedAt = DateTime.UtcNow;
        }

        public bool IsExpired()
        {
            return DateTime.UtcNow > ExpiresAt;
        }

        public void ChangeRole(AccessRole newRole)
        {
            if (AcceptedAt.HasValue)
            {
                throw new InvalidOperationException("Cannot change role of an accepted invitation.");
            }

            if (IsExpired())
            {
                throw new InvalidOperationException("Cannot change role of an expired invitation.");
            }

            Role = newRole;
        }

        public void Revoke()
        {
            if (AcceptedAt.HasValue)
            {
                throw new InvalidOperationException("Cannot revoke an accepted invitation.");
            }

            // Mark as expired by setting ExpiresAt to a past date
            ExpiresAt = DateTime.UtcNow.AddDays(-1);
        }
    }
}
