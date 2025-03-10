using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenEdAI.Models
{
    public class Lesson : BaseEntity
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
        
        // Constructors

        internal Lesson() { } // EF Core required

        public Lesson(string title, string description, string contentLink, List<string>tags, int courseID)
        {
            Title = title;
            Description = description;
            ContentLink = contentLink;
            Tags = tags;
            CourseID = courseID;
        }

        // Update Method
        public void UpdateLesson(string title, string description, List<string> tags, string contentLink)
        {
            Title = title;
            Description = description;
            ContentLink = contentLink;
            Tags = tags;
        }
    }
}
