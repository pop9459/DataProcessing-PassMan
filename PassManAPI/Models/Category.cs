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
            new Category { Id = 1, Name = "Banking", Description = "Bank accounts and financial services" },
            new Category { Id = 2, Name = "Social Media", Description = "Social networks and platforms" },
            new Category { Id = 3, Name = "Email", Description = "Email accounts" },
            new Category { Id = 4, Name = "Work", Description = "Work-related accounts" },
            new Category { Id = 5, Name = "Shopping", Description = "Online shopping accounts" },
            new Category
            {
                Id = 6,
                Name = "Entertainment",
                Description = "Streaming and entertainment services",
            },
            new Category { Id = 7, Name = "Gaming", Description = "Gaming accounts" },
            new Category { Id = 8, Name = "Other", Description = "Other accounts" },
        };

        // Navigation property
        public virtual ICollection<Credential> Credentials { get; set; } = new List<Credential>();
    }
}
