namespace OpenEdAI.API.DTOs
{
    public class StudentDTO
    {
        public string UserID { get;  set; }
        public string Username { get;  set; }
        public bool HasCompletedSetup { get; set; }

        // Only get the IDs for related entities
        public List<int> EnrolledCourseIds { get;  set; }
        public List<int> CreatorCourseIds { get;  set; }
        public List<int> ProgressRecordIds { get;  set; }

    }
}
