namespace OpenEdAI.API.DTOs
{
    public class CreateLessonDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> ContentLinks { get; set; }
        public List<string> Tags { get; set; }
        public int CourseID { get; set; }
    }
}
