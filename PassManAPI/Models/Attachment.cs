using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public class Attachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CredentialId { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public byte[] EncryptedSymmetricKey { get; set; } = Array.Empty<byte>();

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("CredentialId")]
        public virtual Credential Credential { get; set; } = null!;

        // Constructor
        public Attachment(int credentialId, string filePath, byte[] encryptedSymmetricKey, string originalFileName)
        {
            CredentialId = credentialId;
            FilePath = filePath;
            EncryptedSymmetricKey = encryptedSymmetricKey;
            OriginalFileName = originalFileName;
            CreatedAt = DateTime.UtcNow;
        }

        // Default constructor for EF Core
        public Attachment() { }

        // Operations (Methods)
        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("File name cannot be null or whitespace.", nameof(newName));
            }

            if (newName.Length > 255)
            {
                throw new ArgumentException("File name cannot exceed 255 characters.", nameof(newName));
            }

            OriginalFileName = newName;
        }

        public void UpdateFilePath(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(newPath));
            }

            if (newPath.Length > 500)
            {
                throw new ArgumentException("File path cannot exceed 500 characters.", nameof(newPath));
            }

            FilePath = newPath;
        }

        public void UpdateEncryptionKey(byte[] newEncryptionKey)
        {
            if (newEncryptionKey == null || newEncryptionKey.Length == 0)
            {
                throw new ArgumentException("Encryption key cannot be null or empty.", nameof(newEncryptionKey));
            }

            EncryptedSymmetricKey = newEncryptionKey;
        }
    }
}
