namespace OpenEdAI.Client.Models
{
    public class SubmitCourseRequest
    {
        public CoursePersonalizationInput UserInput { get; set; }
        public CoursePlanDTO Plan { get; set; }
    }
}
