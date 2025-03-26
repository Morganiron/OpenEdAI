namespace OpenEdAI.Client.Models
{
    public class CourseDTO
    {
        public int CourseID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public List<int> LessonIds { get; set; }
        public List<EnrolledStudentDTO> EnrolledStudents { get; set; }
    }

    public class EnrolledStudentDTO
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
    }
}
