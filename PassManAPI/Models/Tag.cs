using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public class Tag
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Foreign key for user ownership (tags are user-scoped)
        [Required]
        public int UserId { get; set; }

        // Navigation property for user
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // Navigation property for many-to-many relationship with Credentials
        public virtual ICollection<CredentialTag> CredentialTags { get; set; } = new List<CredentialTag>();

        // Default constructor
        public Tag() { }

        // Constructor with name parameter (from UML specification)
        public Tag(string name)
        {
            Name = name;
        }

        // Constructor with name and userId
        public Tag(string name, int userId)
        {
            Name = name;
            UserId = userId;
        }

        // Rename method (from UML specification)
        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Tag name cannot be empty", nameof(newName));

            Name = newName;
        }
    }
}
