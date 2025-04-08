using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenEdAI.API.Models
{
    public class StudentProfile : BaseEntity
    {
        [Key]
        public int ProfileId { get; set; }

        // Foreign key to the Student
        [Required]
        public string UserId { get; set; }

        // Navigation property to the Student
        [ForeignKey("UserId")]
        public virtual Student Student { get; set; }

        // Reusable profile data and preferences

        // Education level - e.g. Middle School, High School, Bachelors, Masters, PhD, etc
        public string EducationLevel { get; set; } = string.Empty;
        // Preferred Content Types / Learning Style - comma separated string
        public string PreferredContentTypes { get; set; } = string.Empty;
        // Special Considerations - list of terms like "ADHD", "Autism", "Dyslexia", etc
        public string SpecialConsiderations { get; set; } = string.Empty;
        // Additional details for special considerations (optional paragraph added by the user)
        public string AdditionalConsiderations { get; set; } = string.Empty;

    }
}
