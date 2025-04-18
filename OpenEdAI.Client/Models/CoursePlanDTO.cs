namespace OpenEdAI.Client.Models
{
    public class CoursePlanDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public List<LessonPlanDTO> Lessons { get; set; }
    }

    public class LessonPlanDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
    }
}
