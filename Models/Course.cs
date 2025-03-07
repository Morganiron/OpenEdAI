using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenEdAI.Models
{
    public class Course : BaseEntity
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

        [ForeignKey("Student")]
        public string UserID { get; private set; } // Foreign key to Student

        [Required]
        [StringLength(100)]
        public string UserName { get; private set; } // Stores the name of the user who created the course
        public virtual Student Creator { get; private set; } // Creator of the course

        public virtual ICollection<Student> EnrolledStudents { get; private set; } = new List<Student>(); // Students enrolled in the course



        // Base Constructor
        internal Course() { } // EF Core required

        // Constructor for creating a new course
        public Course(string title, string description, List<string> tags, string userId, string userName)
        {
            Title = title;
            Description = description;
            Tags = tags;
            UserID = userId;
            UserName = userName;
        }

        public void UpdateCourse(string title, string description, List<string> tags)
        {
            Title = title;
            Description = description;
            Tags = tags;
        }

        public void ReassignCreator(string newUserId, string newUserName)
        {
            UserID = newUserId;
            UserName = newUserName;
        }


    }
}
