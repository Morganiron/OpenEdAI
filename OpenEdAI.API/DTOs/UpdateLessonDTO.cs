namespace OpenEdAI.API.DTOs
{
    public class UpdateLessonDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> ContentLinks { get; set; }
        public List<string> Tags { get; set; }
    }
}
