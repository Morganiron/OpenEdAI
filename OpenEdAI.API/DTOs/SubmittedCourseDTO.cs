using System.ComponentModel.DataAnnotations;

namespace OpenEdAI.API.DTOs
{
    public class SubmittedCourseDTO
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public List<SubmittedLessonDTO> Lessons { get; set; }
    }

    public class SubmittedLessonDTO
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        // Tags and content links are not required at this stage
    }
}
