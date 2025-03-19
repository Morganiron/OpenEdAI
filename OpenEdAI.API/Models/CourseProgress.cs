using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenEdAI.API.Models
{
    public class CourseProgress : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProgressID { get; private set; }

        [ForeignKey("Student")]
        public string UserID { get; private set; } // AWS Cognito UserID or 'sub'

        [Required]
        [StringLength(100)]
        public string UserName { get; private set; }

        public virtual Student Student { get; private set; }

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
                // Math.Floor will drop any decimals without rounding up or down.
                return Math.Floor((double)LessonsCompleted / Course.TotalLessons * 100);
            }
        }

        // Constructor
        internal CourseProgress() { } // EF Core required

        public CourseProgress(string userId, string userName, int courseID)
        {
            UserID = userId;
            UserName = userName;
            CourseID = courseID;
            LessonsCompleted = 0;
            CompletedLessons = new List<int>();
        }

        // Update progress
        public void MarkLessonCompleted(int lessonID)
        {
            // Get the current list of completed lessons
            List<int> current;
            if (string.IsNullOrEmpty(CompletedLessonsJson))
            {
                current = new List<int>();
            }
            else
            {
                current = JsonSerializer.Deserialize<List<int>>(CompletedLessonsJson);
            }
            

            if (!current.Contains(lessonID))
            {
                current.Add(lessonID);
                LessonsCompleted++;
                UpdateDate = DateTime.UtcNow;

                //Reassign the property so that the JSON is updated
                CompletedLessonsJson = JsonSerializer.Serialize(current);
            }
        }

    }
}
