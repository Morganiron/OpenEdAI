using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenEdAI.Models
{
    public class CourseProgress
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProgressID { get; private set; }

        [ForeignKey("User")]
        public string Owner { get; private set; } // AWS Cognito UserID

        [Required]
        [StringLength(100)]
        public string UserName { get; private set; }

        public virtual User User { get; private set; }

        [ForeignKey("Course")]
        public int CourseID { get; private set; }
        public virtual Course Course { get; private set; }

        public int LessonsCompleted { get; private set; } // Track number of lessons completed from course

        // JSON column for complted lessons
        public string CompletedLessonsJson { get; private set; }

        [NotMapped] // Exclude list from EF Core mapping, handled manually
        public List<int> CompletedLessons
        {   // If CompletedLessonsJson does not exist, create a new List
            get => string.IsNullOrEmpty(CompletedLessonsJson) 
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(CompletedLessonsJson);// If it does exist, deserialize the JSON
            
            private set => CompletedLessonsJson = JsonSerializer.Serialize(value); // Serialize the list to JSON
        }

        public double CompletionPercentage
        {
            get
            {
                if (Course == null || Course.TotalLessons == 0) return 0;
                return (double)LessonsCompleted / Course.TotalLessons * 100;
            }
        }

        public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

        
        // Constructor
        public CourseProgress(string owner, string userName, int courseID)
        {
            Owner = owner;
            UserName = userName;
            CourseID = courseID;
            LessonsCompleted = 0;
            CompletedLessons = new List<int>();
        }

        // Update progress
        public void MarkLessonCompleted(int lessonID)
        {
            if (!CompletedLessons.Contains(lessonID))
            {
                CompletedLessons.Add(lessonID);
                LessonsCompleted++;
                LastUpdated = DateTime.UtcNow;
            }
        }

    }
}
