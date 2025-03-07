namespace OpenEdAI.DTOs
{
    public class CourseProgressDTO
    {
        public int ProgressID { get;  set; }
        public string UserID { get;  set; }
        public string UserName { get;  set; }
        public int CourseID { get;  set; }
        public int LessonsCompleted { get;  set; }

        // Deserialized list of completed lesson IDs
        public required List<int> CompletedLessons { get;  set; }

        public double CompletionPercentage { get;  set; }
        public DateTime LastUpdated { get;  set; }
    }
}
