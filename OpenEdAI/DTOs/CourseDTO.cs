namespace OpenEdAI.DTOs
{
    public class CourseDTO
    {
        public int CourseID { get;  set; }
        public string Title { get;  set; }
        public string Description { get; set; }
        public List<string> Tags { get;  set; }
        public string UserID { get;  set; } // Creator's ID
        public string UserName { get;  set; }
        public DateTime CreatedDate { get;  set; }
        public DateTime UpdateDate { get;  set; }
        
        // Only get the IDs of Lessons related to the course
        public List<int> LessonIds { get;  set; }
        public List<EnrolledStudentDTO> EnrolledStudents { get; set; }
    }

    public class EnrolledStudentDTO
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
    }
}
