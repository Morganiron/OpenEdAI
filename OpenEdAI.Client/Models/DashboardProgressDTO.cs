namespace OpenEdAI.Client.Models
{
    public class DashboardProgressDTO
    {
        public int CourseID { get; set; }
        public string CourseTitle { get; set; } = "";
        public int LessonsCompleted { get; set; }
        public int TotalLessons { get; set; }
        public double CompletionPercentage { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
