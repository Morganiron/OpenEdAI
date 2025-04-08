using System.ComponentModel.DataAnnotations;

namespace OpenEdAI.API.Models
{
    public class Student : BaseEntity
    {
        [Key]
        public string UserID { get; private set; } // Cognito 'sub' (UUID)

        [Required]
        [StringLength(100)]
        public string UserName { get; private set; }

        // Flag to track if the student has completed setup
        public bool HasCompletedSetup { get; private set; } = false;

        // One-to-Many: Courses this student has created
        public virtual ICollection<Course> CreatorCourses { get; private set; } = new List<Course>();
        // Many-to-Many: Courses this student is enrolled in
        public virtual ICollection<Course> EnrolledCourses { get; private set; } = new List<Course>();
        public virtual ICollection<CourseProgress> ProgressRecords { get; private set; } = new List<CourseProgress>();


        // Default Constructor
        internal Student() { }


        public Student(string userId, string name)
        {
            UserID = userId ?? throw new ArgumentException(nameof(userId)); // Prevent null values
            UserName = name;
            HasCompletedSetup = false;
        }

        public void UpdateName(string newName)
        {
            UserName = newName;
        }

        public void MarkSetupComplete()
        {
            HasCompletedSetup = true;
        }
    }
}
