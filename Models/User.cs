using System.ComponentModel.DataAnnotations;

namespace OpenEdAI.Models
{
    public class User
    {
        [Key]
        public string UserID { get; private set; } // Cognito 'sub' (UUID)

        [Required]
        [StringLength(100)]
        public string Name { get; private set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; private set; }

        [Required]
        public string Role { get; private set; } // "Student", "Admin"

        public void UpdateName(string newName)
        {
            Name = newName;
        }

    }
}
