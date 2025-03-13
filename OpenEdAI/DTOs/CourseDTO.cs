using System.ComponentModel.DataAnnotations;

namespace OpenEdAI.DTOs
{
    public class CourseDTO
    {
        [Required]
        public int CourseID { get;  set; }
        [Required]
        public string Title { get;  set; }
        public string Description { get; set; }
        public List<string> Tags { get;  set; }
        [Required]
        public string UserID { get;  set; } // Creator's ID
        [Required]
        public string UserName { get;  set; }
        public DateTime CreatedDate { get;  set; }
        public DateTime UpdateDate { get;  set; }
        
        // Only get the IDs of Lessons related to the course
        public List<int> LessonIds { get;  set; }
        public List<EnrolledStudentDTO> EnrolledStudents { get; set; }
    }

    public class EnrolledStudentDTO
    {
        [Required]
        public string UserID { get; set; }
        [Required]
        public string UserName { get; set; }
    }
}
