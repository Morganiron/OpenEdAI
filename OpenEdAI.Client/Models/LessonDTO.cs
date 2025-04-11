namespace OpenEdAI.Client.Models
{
    public class LessonDTO
    {
        public int LessonID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ContentLink { get; set; }

        public List<string> Tags { get; set; }

        public int CourseID { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}
