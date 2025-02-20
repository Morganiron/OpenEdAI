using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenEdAI.Models
{
    public class CourseProgress
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProgessID { get; private set; }

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

        public double CompletionPercentage => (double)LessonsCompleted / Course.TotalLessons * 100;

        public virtual ICollection<int> CompletedLessons { get; private set; } = new List<int>(); // Stores completed LessonIDs

        public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

        
        // Constructor
        public CourseProgress(string owner, string userName, int courseID)
        {
            Owner = owner;
            UserName = userName;
            CourseID = courseID;
            LessonsCompleted = 0;
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
