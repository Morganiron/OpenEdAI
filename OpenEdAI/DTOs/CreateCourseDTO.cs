using System.ComponentModel.DataAnnotations;

namespace OpenEdAI.DTOs
{
    public class CreateCourseDTO
    {
        [Required]
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        // These will be set by the front-end from the student's token
        [Required]
        public string UserID { get; set; }
        [Required]
        public string UserName { get; set; }
    }
}
