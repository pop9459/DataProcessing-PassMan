using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PassManAPI.Models
{
    public class CredentialTag
    {
        // Composite primary key (configured in DbContext)
        [Key, Column(Order = 0)]
        public int CredentialId { get; set; }

        [Key, Column(Order = 1)]
        public int TagId { get; set; }

        // Navigation properties
        [ForeignKey("CredentialId")]
        public virtual Credential Credential { get; set; } = null!;

        [ForeignKey("TagId")]
        public virtual Tag Tag { get; set; } = null!;

        // Default constructor
        public CredentialTag() { }

        // Constructor with parameters (from UML specification)
        public CredentialTag(int credentialId, int tagId)
        {
            CredentialId = credentialId;
            TagId = tagId;
        }

        // Add credential to this tag relationship (from UML specification)
        public void AddCredential(Credential credential)
        {
            Credential = credential;
            CredentialId = credential.Id;
        }

        // Remove credential from this tag relationship (from UML specification)
        public void RemoveCredential(Credential credential)
        {
            if (CredentialId == credential.Id)
            {
                Credential = null!;
                CredentialId = 0;
            }
        }
    }
}
