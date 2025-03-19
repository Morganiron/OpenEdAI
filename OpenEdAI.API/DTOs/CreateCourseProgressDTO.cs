namespace OpenEdAI.API.DTOs
{
    public class CreateCourseProgressDTO
    {
        // Supplied by the front-end based on the authenticated student
        public string UserID { get; set; }
        public string UserName { get; set; }
        public int CourseID { get; set; }
    }
}
