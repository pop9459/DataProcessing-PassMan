using System.ComponentModel.DataAnnotations;
using PassManAPI.Models;

namespace PassManAPI.DTOs
{
    public class CreateInvitationRequest
    {
        [Required]
        public int VaultId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(View|Edit|Admin)$", ErrorMessage = "Invalid role. Must be View, Edit, or Admin.")]
        public string Role { get; set; } = "View";
    }

    public class InvitationResponse
    {
        public int Id { get; set; }
        public int VaultId { get; set; }
        public string VaultName { get; set; } = string.Empty;
        public string InvitedEmail { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Pending, Accepted, Expired
        public DateTime ExpiresAt { get; set; }
        public string InviteToken { get; set; } = string.Empty; // Returning for testing/simplicity, normally emailed
    }
}
