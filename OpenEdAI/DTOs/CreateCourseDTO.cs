namespace OpenEdAI.DTOs
{
    public class CreateCourseDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        // These will be set by the front-end from the student's token
        public string UserID { get; set; }
        public string UserName { get; set; }
    }
}
