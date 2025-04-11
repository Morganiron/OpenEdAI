namespace OpenEdAI.Client.Models
{
    public class CoursePlanDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<LessonDTO> Lessons { get; set; }
    }
}
