using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenEdAI.Models
{
    public class Lesson
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LessonID { get; private set; }

        [Required]
        [StringLength(200)]
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string ContentLink { get; private set; } // External resource link
        public List<string> Tags { get; private set; } = new List<string>();

        [ForeignKey("Course")]
        public int CourseID { get; private set; }
        public virtual Course Course { get; private set; } // Ensures the lesson is linked to the Course

        [ForeignKey("User")]
        public string Owner { get; private set; } // AWS Cognito UserID

        [Required]
        [StringLength(100)]
        public string UserName { get; private set; }
        public virtual User User { get; private set; }

        public DateTime CreatedDate { get; private set; } = DateTime.UtcNow;
        public DateTime UpdateDate { get; private set; } = DateTime.UtcNow;

        
        // Constructors

        private Lesson() { } // EF Core required

        public Lesson(string title, string description, string contentLink, int courseID, string owner, string userName)
        {
            Title = title;
            Description = description;
            ContentLink = contentLink;
            CourseID = courseID;
            Owner = owner;
            UserName = userName;
        }

        // Update Method
        public void UpdateLesson(string title, string description, List<string> tags, string contentLink)
        {
            Title = title;
            Description = description;
            ContentLink = contentLink;
            Tags = tags;
            UpdateDate = DateTime.UtcNow;
        }
    }
}
