namespace OpenEdAI.API.DTOs
{
    public class CoursePersonalizationInput
    {
        public string Topic { get; set; }
        public string ExperienceLevel { get; set; }
        public string? AdditionalContext { get; set; }
    }
}
