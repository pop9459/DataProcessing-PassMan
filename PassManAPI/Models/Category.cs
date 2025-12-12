using System.ComponentModel.DataAnnotations;

namespace PassManAPI.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Default categories (optional seed data)
        public static readonly List<Category> DefaultCategories = new()
        {
            new Category { Name = "Banking", Description = "Bank accounts and financial services" },
            new Category { Name = "Social Media", Description = "Social networks and platforms" },
            new Category { Name = "Email", Description = "Email accounts" },
            new Category { Name = "Work", Description = "Work-related accounts" },
            new Category { Name = "Shopping", Description = "Online shopping accounts" },
            new Category
            {
                Name = "Entertainment",
                Description = "Streaming and entertainment services",
            },
            new Category { Name = "Gaming", Description = "Gaming accounts" },
            new Category { Name = "Other", Description = "Other accounts" },
        };

        // Navigation property
        public virtual ICollection<Credential> Credentials { get; set; } = new List<Credential>();
    }
}
