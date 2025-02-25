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


        // Default constructor
        public User() { }


        public User(string userId, string name, string email, string role)
        {
            UserID = userId ?? throw new ArgumentException(nameof(userId)); // Prevent null values
            Name = name;
            Email = email ?? throw new ArgumentException(nameof(email));
            Role = role ?? throw new ArgumentException(nameof(role));
        }

        public void UpdateName(string newName)
        {
            Name = newName;
        }

    }
}
