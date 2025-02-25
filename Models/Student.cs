namespace OpenEdAI.Models
{
    public class Student : User
    {
        public virtual ICollection<Course> Courses { get; private set; } = new List<Course>();
        public virtual ICollection<CourseProgress> ProgressRecords { get; private set; } = new List<CourseProgress>();


        // Default Constructor
        public Student() : base() { }


        // Constructor, calling base class(User) constructor
        public Student(string userId, string name, string email) : base(userId, name, email, "Student")
        {

        }
    }
}
