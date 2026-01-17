using System.ComponentModel.DataAnnotations;

namespace PassManAPI.Models
{
    public class Tag
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Navigation property for many-to-many relationship with Credentials
        public virtual ICollection<CredentialTag> CredentialTags { get; set; } = new List<CredentialTag>();

        // Default constructor
        public Tag() { }

        // Constructor with name parameter (from UML specification)
        public Tag(string name)
        {
            Name = name;
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
