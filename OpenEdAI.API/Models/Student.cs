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

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; private set; }


        // One-to-Many: Courses this student has created
        public virtual ICollection<Course> CreatorCourses { get; private set; } = new List<Course>();
        // Many-to-Many: Courses this student is enrolled in
        public virtual ICollection<Course> EnrolledCourses { get; private set; } = new List<Course>();
        public virtual ICollection<CourseProgress> ProgressRecords { get; private set; } = new List<CourseProgress>();


        // Default Constructor
        internal Student() { }


        public Student(string userId, string name, string email)
        {
            UserID = userId ?? throw new ArgumentException(nameof(userId)); // Prevent null values
            UserName = name;
            Email = email ?? throw new ArgumentException(nameof(email));
        }

        public void UpdateName(string newName)
        {
            UserName = newName;
        }

        public void UpdateEmail(string newEmail)
        {
            Email = newEmail;
        }
    }
}
