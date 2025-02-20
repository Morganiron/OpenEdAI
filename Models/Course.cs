using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenEdAI.Models
{
    public class Course
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] //Auto-generate and Auto-increment course IDs
        public int CourseID { get; private set; }

        [Required]
        [StringLength(200)]
        public string Title { get; private set; }

        public string Description { get; private set; }

        public List<string> Tags { get; private set; } = new List<string>();

        public virtual ICollection<Lesson> Lessons { get; private set; } = new List<Lesson>();

        public int TotalLessons => Lessons.Count; // Calculated dynamically

        [ForeignKey("User")]
        public string Owner { get; private set; } // AWS Cognito UserID

        [Required]
        [StringLength(100)]
        public string CreatedBy { get; private set; } // Stores User.Name at the time of creation
        public virtual User User { get; private set; } // Navigation property for additional data

        public DateTime CreatedDate { get; private set; } = DateTime.UtcNow;
        public DateTime UpdateDate { get; private set; } = DateTime.UtcNow;


        // Constructor
        public Course(string title, string description, List<string> tags, string owner, string createdBy)
        {
            Title = title;
            Description = description;
            Tags = tags;
            Owner = owner;
            CreatedBy = createdBy;
        }

        public void UpdateCourse(string title, string description, List<string> tags)
        {
            Title = title;
            Description = description;
            Tags = tags;
            UpdateDate = DateTime.UtcNow;
        }


    }
}
