using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Data;
using OpenEdAI.API.Models;
using OpenEdAI.API.Models;


namespace OpenEdAI.Tests.TestHelpers
{
    public static class InMemoryDbContextFactory
    {
        public static ApplicationDbContext Create()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Use a unique DB for each test
                .Options;

            var context = new ApplicationDbContext(options);

            // Seed data
            SeedTestData(context);

            return context;
        }

        private static void SeedTestData(ApplicationDbContext context)
        {
            // Ensure the database is created
            context.Database.EnsureCreated();

            // Only seed if there's no data
            if (context.Students.Any())
            {
                return; // Database already seeded
            }

            // Seed Students
            var students = new List<Student>
            {
                // Dummy user to inherit courses when a student is deleted from the database
                new Student("00000000-0000-0000-0000-000000000000", "Deleted User"),
                // Actual test students
                new Student("student-001", "Student One"),
                new Student("student-002", "Student Two"),
                new Student("student-003", "Student Three")
            };
            context.Students.AddRange(students);
            context.SaveChanges();

            // Seed Courses
            var courses = new List<Course>
            {
                // Course 1: Unshared Empty Course (created by student-002)
                new Course("Empty Course", "This course has no lessons.", new List<string> { "emptycourse" }, "student-002", "Student Two"),
            
                // Course 2: Shared course With Lessons (created by student-002)
                new Course("Shared course With Lessons", "This course has lessons.", new List<string> { "shared", "haslessons" }, "student-002", "Student Two"),
            
                // Course 3: Unshared Course with Lessons (created by student-003)
                new Course("Exclusive Course", "This course is not shared.", new List<string> { "notshared", "haslessons" }, "student-003", "Student Three")
            };
            context.Courses.AddRange(courses);
            context.SaveChanges();

            // Seed Lessons
            var lessons = new List<Lesson>
        {
            // For course with CourseID = 2 (Course With Lessons)
            new Lesson("Lesson 1", "Introduction", "https://example.com/lesson1", new List<string>{"intro"}, courses[1].CourseID),
            new Lesson("Lesson 2", "Advanced Topic", "https://example.com/lesson2", new List<string>{"advanced", "c#"}, courses[1].CourseID),
            new Lesson("Lesson 3", "Final Thoughts", "https://example.com/lesson3", new List<string>{"final"}, courses[1].CourseID),
            
            // For course with CourseID = 3 (Exclusive Course)
            new Lesson("Lesson 4", "Basics", "https://example.com/lessonA", new List<string>{"C#", "basics"}, courses[2].CourseID),
            new Lesson("Lesson 5", "Deep Dive", "https://example.com/lessonB", new List<string>{"deepdive", "intermediate"}, courses[2].CourseID),
            new Lesson("Lesson 6", "Summary", "https://example.com/lessonC", new List<string>{"summary", ".net"}, courses[2].CourseID)
        };
            context.Lessons.AddRange(lessons);
            context.SaveChanges();

            // Seed CourseEnrollments
            // Note: In EF Core many-to-many, you add students to course.EnrolledStudents
            // Course 1: Only student-002 is enrolled.
            courses[0].EnrolledStudents.Add(students.Single(s => s.UserID == "student-002"));
            // Course 2: Both student-002 and student-003 are enrolled.
            courses[1].EnrolledStudents.Add(students.Single(s => s.UserID == "student-002"));
            courses[1].EnrolledStudents.Add(students.Single(s => s.UserID == "student-003"));
            // Course 3: Only student-003 is enrolled.
            courses[2].EnrolledStudents.Add(students.Single(s => s.UserID == "student-003"));
            context.SaveChanges();

            // Course Progress record for course 2 (courses[1]) with 1 lesson completed(lessons[0])
            var progress1 = new CourseProgress("student-003", "Student Three", courses[1].CourseID);
            // Call MarkLessonCompleted to update the progress.
            progress1.MarkLessonCompleted(lessons[0].LessonID);

            // Course Progress record for course 3 (courses[2]) with 2 lessons completed(lessons[3], lessons[4])
            var progress2 = new CourseProgress("student-003", "Student Three", courses[2].CourseID);
            // Call MarkLessonCompleted to update the progress.
            progress2.MarkLessonCompleted(lessons[3].LessonID);
            progress2.MarkLessonCompleted(lessons[4].LessonID);

            context.CourseProgress.AddRange(progress1, progress2);
            context.SaveChanges();

        }
    }
}
