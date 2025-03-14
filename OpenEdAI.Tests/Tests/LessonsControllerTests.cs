using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenEdAI.Tests.TestHelpers;
using Xunit;
using OpenEdAI.Models;
using OpenEdAI.Controllers;
using OpenEdAI.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                HttpContext = new DefaultHttpContext { User = GetMockUser() }
            };
        }

        // ==================== GET Tests ====================

        [Fact]
        public async Task GetLessons_ReturnsListOfLessons()
        {
            // Act: Call the GetLessons method
            var result = await _controller.GetLessons();

            // Assert: Ensure the respons is OkObjectResult containing a list of LessonDTOs
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var lessons = Assert.IsAssignableFrom<IEnumerable<LessonDTO>>(okResult.Value);
            Assert.NotEmpty(lessons);
        }

        [Fact]
        public async Task GetLesson_ValidId_ReturnsLesson()
        {
            // Arrange: Retrieve an existing lesson from the database
            var lesson = _context.Lessons.FirstOrDefault();
            Assert.NotNull(lesson);

            // Act: Call GetLesson with a valid lesson ID
            var result = await _controller.GetLesson(lesson.LessonID);

            // Assert: Verify that the response contains the correct lesson
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedLesson = Assert.IsType<LessonDTO>(okResult.Value);
            Assert.Equal(lesson.LessonID, returnedLesson.LessonID);
        }

        [Fact]
        public async Task GetLesson_InvalidId_ReturnsBadRequest()
        {
            // Act: Call GetLesson with an invalid LessonID (-1)
            var result = await _controller.GetLesson(-1);

            // Assert: Ensure the response is BadRequest with the correct message
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Invalid lesson ID", badRequestResult.Value);
        }

        [Fact]
        public async Task GetLessonsByCourse_ValidCourseId_ReturnsLessons()
        {
            // Arrange: Retrieve a course that has lessons
            var course = _context.Courses.Include(c => c.Lessons).FirstOrDefault(c => c.Lessons.Any());
            Assert.NotNull(course);

            // Act: Call GetLessonsByCourse with a valid course ID
            var result = await _controller.GetLessonsByCourse(course.CourseID);

            // Assert: Ensure the response contains a list of lessons
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var lessons = Assert.IsAssignableFrom<IEnumerable<LessonDTO>>(okResult.Value);
            Assert.NotEmpty(lessons);
        }

        [Fact]
        public async Task GetLessonsByCourse_InvalidCourseId_ReturnsNotFound()
        {
            // Act: Call GetLessonsByCourse with an invalid CourseID (-1)
            var result = await _controller.GetLessonsByCourse(-1);

            //Assert: Ensure the response is NotFound with the correct message
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("No lessons found for this course", notFoundResult.Value);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateLesson_ValidData_ReturnsCreatedLesson()
        {
            // Arrange: Retrieve an existing course (required for lesson creation)
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            // Create a valid lesson DTO
            var createDto = new CreateLessonDTO
            {
                Title = "New Lesson",
                Description = "Description",
                ContentLink = "https://example.com",
                Tags = new List<string> { "test" },
                CourseID = course.CourseID
            };

            // Act: Call CreateLesson
            var result = await _controller.CreateLesson(createDto);

            // Assert: Verify that the lesson was created successfully
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var lessonDto = Assert.IsType<LessonDTO>(createdResult.Value);
            Assert.Equal(createDto.Title, lessonDto.Title);
        }

        [Fact]
        public async Task CreateLesson_InvalidCourse_ReturnsBadRequest()
        {
            // Arrange: Create a lesson DTO with a non-existent CourseID
            var createDto = new CreateLessonDTO
            {
                Title = "Lesson",
                Description = "Description",
                ContentLink = "https://example.com/lesson",
                Tags = new List<string> { "test" },
                CourseID = -1
            };

            // Act: Attempt to create a lesson with an invalid CourseID
            var result = await _controller.CreateLesson(createDto);

            // Assert: Ensure the response is BadRequest with the appropriate message
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Course not found", badRequestResult.Value);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task UpdateLesson_ValidData_ReturnsNoContent()
        {
            // Arrange: Retrieve an existing lesson and create an update DTO
            var lesson = _context.Lessons.FirstOrDefault();
            Assert.NotNull(lesson);

            var updateDto = new UpdateLessonDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                ContentLink = "https://example.com/updatedLesson",
                Tags = new List<string> { "updated" }
            };

            // Act: Attempt to update the lesson
            var result = await _controller.UpdateLesson(lesson.LessonID, updateDto);

            // Assert: Ensure the response is NoContent and the Lessons is updated
            Assert.IsType<NoContentResult>(result);
            var updatedLesson = await _context.Lessons.FindAsync(lesson.LessonID);
            Assert.NotNull(updatedLesson);
            Assert.Equal("Updated Title", updatedLesson.Title);
        }

        [Fact]
        public async Task UpdateLesson_InvalidId_ReturnsNotFound()
        {
            // Arrange: Create an update DTO
            var updateDto = new UpdateLessonDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                ContentLink = "https://example.com/updatedLesson",
                Tags = new List<string> { "updated" }
            };

            // Act: Attempt to update a lesson with an invalid lesson ID
            var result = await _controller.UpdateLesson(-1, updateDto);

            // Assert: Ensure response is NotFound
            var notFoundResult = Assert.IsType<NotFoundResult>(result);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task DeleteLesson_ValidId_DeletesLesson()
        {
            // Arrange: Retrieve an existing lesson
            var lesson = _context.Lessons.FirstOrDefault();
            Assert.NotNull(lesson);

            // Act: Attempt to delete the lesson
            var result = await _controller.DeleteLesson(lesson.LessonID);

            // Assert: Ensure NoContent result and the lesson is deleted
            Assert.IsType<NoContentResult>(result);
            var deletedLesson = await _context.Lessons.FindAsync(lesson.LessonID);
            Assert.Null(deletedLesson);
        }

        [Fact]
        public async Task DeleteLesson_InvalidId_ReturnsNotFound()
        {
            // Act: Attempt to delete a lesson with an invalid ID
            var result = await _controller.DeleteLesson(-1);

            // Assert: Ensure the response is NotFound
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
