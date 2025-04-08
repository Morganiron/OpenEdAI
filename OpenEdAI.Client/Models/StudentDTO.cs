namespace OpenEdAI.Client.Models
{
    public class StudentDTO
    {
        public string UserID { get; set; }
        public string Username { get; set; }
        public bool HasCompletedSetup { get; set; }
        public StudentProfileDTO Profile { get; set; }

        public List<int> EnrolledCourseIds { get; set; }
        public List<int> CreatorCourseIds { get; set; }
        public List<int> ProgressRecordIds { get; set; }
    }
}
