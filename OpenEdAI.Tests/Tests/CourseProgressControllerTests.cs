// CourseProgressControllerTests.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;
using OpenEdAI.Tests.TestHelpers;

namespace OpenEdAI.Tests.Tests
{
    public class CourseProgressControllerTests : BaseTest
    {
        private readonly CourseProgressController _controller;

        public CourseProgressControllerTests() : base()
        {
            // Arrange: Initialize controller with a default valid user
            _controller = new CourseProgressController(_context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = GetMockUser("student-003", "Student Three")
                    }
                }
            };
        }

        // ==================== GET ALL ====================

        [Fact]
        public async Task GetProgress_ReturnsAllProgress()
        {
            // Act
            var result = await _controller.GetProgress();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<CourseProgressDTO>>(ok.Value);
            Assert.NotEmpty(list);
        }

        // ==================== GET USER-SPECIFIC ====================

        [Fact]
        public async Task GetUserProgress_NoToken_ReturnsUnauthorized()
        {
            // Arrange: remove user identity
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();

            // Act
            var result = await _controller.GetUserProgress();

            // Assert
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetUserProgress_ValidUser_ReturnsOnlyTheirProgress()
        {
            // Act
            var result = await _controller.GetUserProgress();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<CourseProgressDTO>>(ok.Value);
            Assert.All(list, dto => Assert.Equal("student-003", dto.UserID));
        }

        // ==================== GET BY ID ====================

        [Fact]
        public async Task GetProgressById_ValidId_ReturnsProgress()
        {
            // Arrange
            var existing = _context.CourseProgress.First();

            // Act
            var result = await _controller.GetProgress(existing.ProgressID);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<CourseProgressDTO>(ok.Value);
            Assert.Equal(existing.ProgressID, dto.ProgressID);
        }

        [Fact]
        public async Task GetProgressById_InvalidId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetProgress(-1);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // ==================== CREATE ====================

        [Fact]
        public async Task CreateProgress_ValidRequest_ReturnsCreated()
        {
            // Arrange
            var course = _context.Courses.First();
            var dto = new CreateCourseProgressDTO
            {
                UserID = "student-003",
                UserName = "Student Three",
                CourseID = course.CourseID
            };

            // Act
            var result = await _controller.CreateProgress(dto);

            // Assert
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var ret = Assert.IsType<CourseProgressDTO>(created.Value);
            Assert.Equal(dto.UserID, ret.UserID);
            Assert.Equal(dto.CourseID, ret.CourseID);
            Assert.Empty(ret.CompletedLessons);
            Assert.Equal(0, ret.LessonsCompleted);
        }

        [Fact]
        public async Task CreateProgress_InvalidCourse_ReturnsBadRequest()
        {
            // Arrange
            var dto = new CreateCourseProgressDTO
            {
                UserID = "student-003",
                UserName = "Student Three",
                CourseID = -1
            };

            // Act
            var result = await _controller.CreateProgress(dto);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Course not found", bad.Value);
        }

        [Fact]
        public async Task CreateProgress_WrongUser_ReturnsForbid()
        {
            // Arrange: request userID mismatch
            var course = _context.Courses.First();
            var dto = new CreateCourseProgressDTO
            {
                UserID = "someone-else",
                UserName = "Other",
                CourseID = course.CourseID
            };

            // Act
            var result = await _controller.CreateProgress(dto);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }

        // ==================== PATCH (COMPLETE LESSON) ====================

        [Fact]
        public async Task PatchProgress_ValidCompletion_ReturnsNoContent()
        {
            // Arrange
            var progress = _context.CourseProgress
                .Include(cp => cp.Course).ThenInclude(c => c.Lessons)
                .First(cp => cp.Course.Lessons.Any());
            var initialCount = progress.LessonsCompleted;
            var lesson = progress.Course.Lessons
                .First(l => !progress.CompletedLessons.Contains(l.LessonID));
            var patchDto = new MarkLessonCompleteDTO { LessonID = lesson.LessonID };

            // Act
            var result = await _controller.PatchProgress(progress.ProgressID, patchDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var updated = await _context.CourseProgress.FindAsync(progress.ProgressID);
            Assert.Contains(lesson.LessonID, updated.CompletedLessons);
            Assert.Equal(initialCount + 1, updated.LessonsCompleted);
        }

        [Fact]
        public async Task PatchProgress_InvalidProgressId_ReturnsNotFound()
        {
            // Arrange
            var patchDto = new MarkLessonCompleteDTO { LessonID = 1 };

            // Act
            var result = await _controller.PatchProgress(-1, patchDto);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PatchProgress_InvalidLesson_ReturnsBadRequest()
        {
            // Arrange
            var progress = _context.CourseProgress
                .Include(cp => cp.Course).ThenInclude(c => c.Lessons)
                .First(cp => cp.Course.Lessons.Any());
            var patchDto = new MarkLessonCompleteDTO { LessonID = -1 };

            // Act
            var result = await _controller.PatchProgress(progress.ProgressID, patchDto);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Lesson not found in course", bad.Value);
        }

        [Fact]
        public async Task PatchProgress_WrongUser_ReturnsForbid()
        {
            // Arrange
            var progress = _context.CourseProgress.First();
            _controller.ControllerContext.HttpContext.User = GetMockUser("intruder");
            var patchDto = new MarkLessonCompleteDTO { LessonID = 1 };

            // Act
            var result = await _controller.PatchProgress(progress.ProgressID, patchDto);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== DELETE ====================

        [Fact]
        public async Task DeleteProgress_ValidId_ReturnsNoContent()
        {
            // Arrange
            var course = _context.Courses.First();
            var prog = new CourseProgress("student-003", "Student Three", course.CourseID);
            _context.CourseProgress.Add(prog);
            await _context.SaveChangesAsync();
            var id = prog.ProgressID;

            // Act
            var result = await _controller.DeleteProgress(id);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.Null(await _context.CourseProgress.FindAsync(id));
        }

        [Fact]
        public async Task DeleteProgress_WrongUser_ReturnsForbid()
        {
            // Arrange
            var prog = _context.CourseProgress.First();
            _controller.ControllerContext.HttpContext.User = GetMockUser("other-user");

            // Act
            var result = await _controller.DeleteProgress(prog.ProgressID);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
    }
}
