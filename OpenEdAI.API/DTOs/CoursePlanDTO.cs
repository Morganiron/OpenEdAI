namespace OpenEdAI.API.DTOs
{
    public class CoursePlanDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<LessonPlanDTO> Lessons { get; set; } = new List<LessonPlanDTO>();
    }

    public class LessonPlanDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}
