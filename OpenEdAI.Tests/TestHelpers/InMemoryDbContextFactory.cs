using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Data;
using OpenEdAI.API.Models;

namespace OpenEdAI.Tests.TestHelpers
{
    public static class InMemoryDbContextFactory
    {
        public static ApplicationDbContext Create()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
                .Options;

            var context = new ApplicationDbContext(options);

            // Seed data
            SeedTestData(context);

            return context;
        }

        private static void SeedTestData(ApplicationDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Only seed if no students exist
            if (context.Students.Any()) return;

            // Seed Students
            var students = new List<Student>
            {
                new Student("00000000-0000-0000-0000-000000000000", "Deleted User"),
                new Student("student-001", "Student One"),
                new Student("student-002", "Student Two"),
                new Student("student-003", "Student Three")
            };
            context.Students.AddRange(students);
            context.SaveChanges();

            // Seed StudentProfiles for each student
            var profiles = new List<StudentProfile>
            {
                new StudentProfile
                {
                    UserId = "00000000-0000-0000-0000-000000000000",
                    EducationLevel = "Elementary",
                    PreferredContentTypes = "",
                    SpecialConsiderations = "",
                    AdditionalConsiderations = ""
                },
                new StudentProfile
                {
                    UserId = "student-001",
                    EducationLevel = "Middle School",
                    PreferredContentTypes = "Video tutorials,Articles",
                    SpecialConsiderations = "",
                    AdditionalConsiderations = ""
                },
                new StudentProfile
                {
                    UserId = "student-002",
                    EducationLevel = "Associate's",
                    PreferredContentTypes = "Articles",
                    SpecialConsiderations = "",
                    AdditionalConsiderations = ""
                },
                new StudentProfile
                {
                    UserId = "student-003",
                    EducationLevel = "PhD",
                    PreferredContentTypes = "Discussion forums,Videos",
                    SpecialConsiderations = "",
                    AdditionalConsiderations = ""
                }
            };
            context.StudentProfiles.AddRange(profiles);
            context.SaveChanges();

            // Seed Courses
            var courses = new List<Course>
            {
                new Course("Empty Course", "This course has no lessons.", new List<string> { "emptycourse" }, "student-002", "Student Two"),
                new Course("Shared course With Lessons", "This course has lessons.", new List<string> { "shared", "haslessons" }, "student-002", "Student Two"),
                new Course("Exclusive Course", "This course is not shared.", new List<string> { "notshared", "haslessons" }, "student-003", "Student Three")
            };
            context.Courses.AddRange(courses);
            context.SaveChanges();

            // Seed Lessons
            var lessons = new List<Lesson>
            {
                new Lesson("Lesson 1", "Introduction", new List<string>{"https://example.com/lesson1"}, new List<string>{"intro"}, courses[1].CourseID),
                new Lesson("Lesson 2", "Advanced Topic", new List<string>{"https://example.com/lesson2"}, new List<string>{"advanced","c#"}, courses[1].CourseID),
                new Lesson("Lesson 3", "Final Thoughts", new List<string>{"https://example.com/lesson3"}, new List<string>{"final"}, courses[1].CourseID),
                new Lesson("Lesson 4", "Basics", new List<string>{"https://example.com/lessonA"}, new List<string>{"C#","basics"}, courses[2].CourseID),
                new Lesson("Lesson 5", "Deep Dive", new List<string>{"https://example.com/lessonB"}, new List<string>{"deepdive","intermediate"}, courses[2].CourseID),
                new Lesson("Lesson 6", "Summary", new List<string>{"https://example.com/lessonC"}, new List<string>{"summary",".net"}, courses[2].CourseID)
            };
            context.Lessons.AddRange(lessons);
            context.SaveChanges();

            // Seed CourseEnrollments
            courses[0].EnrolledStudents.Add(students.Single(s => s.UserID == "student-002"));
            courses[1].EnrolledStudents.Add(students.Single(s => s.UserID == "student-002"));
            courses[1].EnrolledStudents.Add(students.Single(s => s.UserID == "student-003"));
            courses[2].EnrolledStudents.Add(students.Single(s => s.UserID == "student-003"));
            context.SaveChanges();

            // Seed CourseProgress records
            var progress1 = new CourseProgress("student-003", "Student Three", courses[1].CourseID);
            progress1.MarkLessonCompleted(lessons[0].LessonID);

            var progress2 = new CourseProgress("student-003", "Student Three", courses[2].CourseID);
            progress2.MarkLessonCompleted(lessons[3].LessonID);
            progress2.MarkLessonCompleted(lessons[4].LessonID);

            context.CourseProgress.AddRange(progress1, progress2);
            context.SaveChanges();
        }
    }
}
