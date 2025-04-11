namespace OpenEdAI.API.DTOs
{
    public class CoursePlanDTO
    {
        public string Title { get; set; }
        public List<LessonDTO> Lessons { get; set; }
    }
}
