using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenEdAI.Data;
using OpenEdAI.Models;
using OpenEdAI.Tests.TestHelpers;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.DTOs;

namespace OpenEdAI.Tests.Tests
{
    public class CourseProgressControllerTests : BaseTest
    {
        private readonly CourseProgressController _controller;

        public CourseProgressControllerTests() : base() 
        {
            _controller = new CourseProgressController(_context);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = GetMockUser() }
            };
        }

        // ==================== GET Tests ====================

        [Fact]
        public async Task GetProgress_ReturnsListOfProgress()
        {
            // Act: Retrieve the list of course progress records
            var result = await _controller.GetProgress();

            // Assert: Ensure the result is OK and that thte list is not empty
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var progressList = Assert.IsAssignableFrom<IEnumerable<CourseProgressDTO>>(okResult.Value);
            Assert.NotEmpty(progressList);
        }

        [Fact]
        public async Task GetProgress_ValidId_ReturnsProgress()
        {
            // Arrange: Get an existing course progress record
            var existingProgress = _context.CourseProgress.FirstOrDefault();
            Assert.NotNull(existingProgress);

            // Act: Retrieve the progress record by ProgressID
            var result = await _controller.GetProgress(existingProgress.ProgressID);

            // Assert: Ensure the returned progress DTO matches the expected ProgressID
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var progressDto = Assert.IsType<CourseProgressDTO>(okResult.Value);
            Assert.Equal(existingProgress.ProgressID, progressDto.ProgressID);
        }

        [Fact]
        public async Task GetProgress_InvalidId_ReturnsNotFound()
        {
            // Act: Attempt to retrieve a progress record with an invalid ID
            var result = await _controller.GetProgress(-1);

            // Assert: Expect a NotFound result
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateProgress_ValidData_ReturnsCreatedProgress()
        {
            // Arrange: Get an existing course for which to create progress
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            var createDto = new CreateCourseProgressDTO
            {
                UserID = "student-001",
                UserName = "Student One",
                CourseID = course.CourseID
            };

            // Act: Create the progress record
            var result = await _controller.CreateProgress(createDto);

            // Assert: Verify that a CreatedAtAction result is returned with correct data
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var progressDto = Assert.IsType<CourseProgressDTO>(createdResult.Value);
            Assert.Equal(createDto.UserID, progressDto.UserID);
            Assert.Equal(createDto.CourseID, progressDto.CourseID);
            Assert.Equal(0, progressDto.LessonsCompleted);
            Assert.Empty(progressDto.CompletedLessons);
        }

        [Fact]
        public async Task CreateProgress_InvalidCourse_ReturnsBadRequest()
        {
            // Arrange: Use an invalid CourseID
            var createDto = new CreateCourseProgressDTO
            {
                UserID = "student-001",
                UserName = "Student One",
                CourseID = -1
            };

            // Act: Attempt to create a progress record for a non-existent course
            var result = await _controller.CreateProgress(createDto);

            // Assert: Expect a BadRequest with the message "Course not found"
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Course not found", badRequestResult.Value);
        }

        // ==================== PATCH Tests ====================

        [Fact]
        public async Task PatchProgress_ValidLesson_CompletesLesson()
        {
            // Arrange: Get a progress record from a course that has a lesson
            var progress = _context.CourseProgress
                .Include(cp => cp.Course)
                .ThenInclude(c => c.Lessons)
                .FirstOrDefault(cp => cp.Course != null && cp.Course.Lessons.Any());
            Assert.NotNull(progress);

            int initialCompleted = progress.LessonsCompleted;

            // Find a lesson in the course that has not yet been completed
            var lesson = progress.Course.Lessons.FirstOrDefault(l => !progress.CompletedLessons.Contains(l.LessonID));
            Assert.NotNull(lesson);

            var patchDto = new MarkLessonCompleteDTO { LessonID = lesson.LessonID };

            // Act: Mark the lesson as complete
            var result = await _controller.PatchProgress(progress.ProgressID, patchDto);

            // Assert: Ensure the patch returns NoContent
            Assert.IsType<NoContentResult>(result);

            // Reload the progress record and verify the lesson was added
            var updatedProgress = await _context.CourseProgress
                .Include(cp => cp.Course)
                .ThenInclude(c => c.Lessons)
                .FirstOrDefaultAsync(cp => cp.ProgressID == progress.ProgressID);

            Assert.NotNull(updatedProgress);
            Assert.Contains(lesson.LessonID, updatedProgress.CompletedLessons);
            Assert.Equal(initialCompleted + 1, updatedProgress.LessonsCompleted);
        }

        [Fact]
        public async Task PatchProgress_InvalidLesson_ReturnsBadRequest()
        {
            // Arrange: Get an existing progress record
            var progress = _context.CourseProgress
                .Include(cp => cp.Course)
                .ThenInclude(c => c.Lessons)
                .FirstOrDefault(cp => cp.Course != null && cp.Course.Lessons.Any());
            Assert.NotNull(progress);

            // Use an invalid LessonID (one not in the course)
            var patchDto = new MarkLessonCompleteDTO { LessonID = -1 };

            // Act: Attempt to mark an invalid lesson as complete
            var result = await _controller.PatchProgress(progress.ProgressID, patchDto);

            // Assert: Expect a BadRequest with the message "Lesson not found in course"
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Lesson not found in course", badRequestResult.Value);
        }

        // ==================== DELETE Tests ====================
        
        [Fact]
        public async Task DeleteProgress_ValidId_DeletesProgress()
        {
            // Arrange: Create a new progress record to ensure that deletion doesn't affect seeded data
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            var progress = new CourseProgress("student-001", "Student One", course.CourseID);
            _context.CourseProgress.Add(progress);
            await _context.SaveChangesAsync();
            int progressId = progress.ProgressID;

            // Act: Delete the progress record
            var result = await _controller.DeleteProgress(progressId);

            // Assert: Ensure the deletion returns NoContent and the record is removed
            Assert.IsType<NoContentResult>(result);
            var deletedProgress = await _context.CourseProgress.FindAsync(progressId);
            Assert.Null(deletedProgress);
        }

    }
}
