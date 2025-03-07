namespace OpenEdAI.DTOs
{
    public class UpdateLessonDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ContentLink { get; set; }
        public List<string> Tags { get; set; }
    }
}
