namespace OpenEdAI.DTOs
{
    public class CreateLessonDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ContentLink { get; set; }
        public List<string> Tags { get; set; }
        public int CourseID { get; set; }
    }
}
