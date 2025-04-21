using System.ComponentModel.DataAnnotations;

namespace OpenEdAI.Client.Models
{
    public class CoursePersonalizationInput
    {
        [Required(ErrorMessage = "Topic is required.")]
        public string Topic { get; set; }
        [Required(ErrorMessage = "Experience level is required.")]
        public string ExperienceLevel { get; set; }
        public string? AdditionalContext { get; set; }
    }
}
