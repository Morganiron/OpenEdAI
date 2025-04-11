namespace OpenEdAI.API.DTOs
{
    public class CoursePlanDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<LessonDTO> Lessons { get; set; }
    }
}
