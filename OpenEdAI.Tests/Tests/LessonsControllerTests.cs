using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.DTOs;
using OpenEdAI.Tests.TestHelpers;

namespace OpenEdAI.Tests.Tests
{
    public class LessonsControllerTests : BaseTest
    {
        private readonly LessonsController _controller;

        public LessonsControllerTests() : base()
        {
            _controller = new LessonsController(_context);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = GetMockUser("student-003", "Student Three") }
            };
        }

        // ==================== GET Tests ====================

        [Fact]
        public async Task GetLessons_ReturnsListOfLessons()
        {
            // Act
            var result = await _controller.GetLessons();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var lessons = Assert.IsAssignableFrom<IEnumerable<LessonDTO>>(ok.Value);
            Assert.NotEmpty(lessons);
        }

        [Fact]
        public async Task GetLesson_ValidId_ReturnsLesson()
        {
            // Arrange
            var lesson = _context.Lessons.First();

            // Act
            var result = await _controller.GetLesson(lesson.LessonID);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<LessonDTO>(ok.Value);
            Assert.Equal(lesson.LessonID, dto.LessonID);
        }

        [Fact]
        public async Task GetLesson_InvalidId_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetLesson(-1);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid lesson ID", bad.Value);
        }

        [Fact]
        public async Task GetLessonsByCourse_ValidCourseId_ReturnsLessons()
        {
            // Arrange
            var course = _context.Courses
                .Include(c => c.Lessons)
                .First(c => c.Lessons.Any());

            // Act
            var result = await _controller.GetLessonsByCourse(course.CourseID);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var lessons = Assert.IsAssignableFrom<IEnumerable<LessonDTO>>(ok.Value);
            Assert.NotEmpty(lessons);
        }

        [Fact]
        public async Task GetLessonsByCourse_InvalidCourseId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetLessonsByCourse(-1);

            // Assert
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("No lessons found for this course", nf.Value);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateLesson_ValidData_ReturnsCreatedLesson()
        {
            // Arrange: pick a course owned by student-003
            var course = _context.Courses.First(c => c.UserID == "student-003");
            var dto = new CreateLessonDTO
            {
                Title = "New Lesson",
                Description = "Description",
                ContentLinks = new List<string> { "https://example.com" },
                Tags = new List<string> { "test" },
                CourseID = course.CourseID
            };

            // Act
            var result = await _controller.CreateLesson(dto);

            // Assert
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returned = Assert.IsType<LessonDTO>(created.Value);
            Assert.Equal(dto.Title, returned.Title);
        }

        [Fact]
        public async Task CreateLesson_InvalidCourse_ReturnsBadRequest()
        {
            // Arrange
            var dto = new CreateLessonDTO
            {
                Title = "Lesson",
                Description = "Description",
                ContentLinks = new List<string> { "https://example.com/lesson" },
                Tags = new List<string> { "test" },
                CourseID = -1
            };

            // Act
            var result = await _controller.CreateLesson(dto);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Course not found", bad.Value);
        }

        [Fact]
        public async Task CreateLesson_NotOwner_ReturnsForbid()
        {
            // Arrange: pick a course NOT owned by student-003
            var course = _context.Courses.First(c => c.UserID != "student-003");
            var dto = new CreateLessonDTO
            {
                Title = "Oh No",
                Description = "Should not pass",
                ContentLinks = new List<string> { "https://x" },
                Tags = new List<string> { "x" },
                CourseID = course.CourseID
            };

            // Act
            var result = await _controller.CreateLesson(dto);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task UpdateLesson_ValidData_ReturnsNoContent()
        {
            // Arrange: pick a lesson under student-003’s course
            var courseId = _context.Courses.First(c => c.UserID == "student-003").CourseID;
            var lesson = _context.Lessons.First(l => l.CourseID == courseId);
            var dto = new UpdateLessonDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                ContentLinks = new List<string> { "https://example.com/updatedLesson" },
                Tags = new List<string> { "updated" }
            };

            // Act
            var result = await _controller.UpdateLesson(lesson.LessonID, dto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var updated = await _context.Lessons.FindAsync(lesson.LessonID);
            Assert.Equal("Updated Title", updated.Title);
        }

        [Fact]
        public async Task UpdateLesson_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var dto = new UpdateLessonDTO
            {
                Title = "X",
                Description = "X",
                ContentLinks = new List<string> { "https://x" },
                Tags = new List<string> { "x" }
            };

            // Act
            var result = await _controller.UpdateLesson(-1, dto);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateLesson_NotOwner_ReturnsForbid()
        {
            // Arrange: pick a lesson under someone else's course
            var otherCourseId = _context.Courses.First(c => c.UserID != "student-003").CourseID;
            var lesson = _context.Lessons.First(l => l.CourseID == otherCourseId);
            var dto = new UpdateLessonDTO
            {
                Title = "Nope",
                Description = "Nope",
                ContentLinks = new List<string> { "https://x" },
                Tags = new List<string> { "x" }
            };

            // Act
            var result = await _controller.UpdateLesson(lesson.LessonID, dto);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== DELETE Tests ====================

        [Fact]
        public async Task DeleteLesson_ValidId_DeletesLesson()
        {
            // Arrange: pick a lesson under student-003’s course
            var courseId = _context.Courses.First(c => c.UserID == "student-003").CourseID;
            var lesson = _context.Lessons.First(l => l.CourseID == courseId);

            // Act
            var result = await _controller.DeleteLesson(lesson.LessonID);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Null(await _context.Lessons.FindAsync(lesson.LessonID));
        }

        [Fact]
        public async Task DeleteLesson_InvalidId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.DeleteLesson(-1);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteLesson_NotOwner_ReturnsForbid()
        {
            // Arrange: pick someone else’s lesson
            var otherCourseId = _context.Courses.First(c => c.UserID != "student-003").CourseID;
            var lesson = _context.Lessons.First(l => l.CourseID == otherCourseId);

            // Act
            var result = await _controller.DeleteLesson(lesson.LessonID);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
    }
}
