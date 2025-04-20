using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;
using OpenEdAI.Tests.TestHelpers;
using Xunit;

namespace OpenEdAI.Tests.Tests
{
    public class CoursesControllerTests : BaseTest
    {
        private readonly CoursesController _controller;

        public CoursesControllerTests() : base()
        {
            // Initialize the controller with the DbContext and mock user (student-003)
            _controller = new CoursesController(_context);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = GetMockUser() }
            };
        }

        // ==================== GET Tests ====================

        [Fact]
        public async Task GetCourses_ReturnsListOfCourses()
        {
            // Act: Retrieve list of courses.
            var result = await _controller.GetCourses();

            // Assert: Verify OK and non-empty list.
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var courses = Assert.IsAssignableFrom<IEnumerable<CourseDTO>>(okResult.Value);
            Assert.NotEmpty(courses);
        }

        [Fact]
        public async Task GetCourse_ValidId_ReturnsCourse()
        {
            // Arrange: pick an existing course.
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            // Act
            var result = await _controller.GetCourse(course.CourseID);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedCourse = Assert.IsType<CourseDTO>(okResult.Value);
            Assert.Equal(course.CourseID, returnedCourse.CourseID);
        }

        [Fact]
        public async Task GetCourse_InvalidId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.GetCourse(-1);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateCourse_ReturnsCreatedCourse()
        {
            // Arrange: create a new course DTO.
            var newCourse = new CreateCourseDTO
            {
                Title = "Test Course",
                Description = "A test description",
                Tags = new List<string> { "test", "csharp" },
                UserID = "student-003",
                UserName = "Student Three"
            };

            // Act
            var result = await _controller.CreateCourse(newCourse);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdCourse = Assert.IsType<CourseDTO>(createdResult.Value);
            Assert.Equal(newCourse.Title, createdCourse.Title);
        }

        [Fact]
        public async Task CreateCourse_MissingTitle_ReturnsBadRequest()
        {
            // Arrange
            var newCourse = new CreateCourseDTO
            {
                Title = null,
                Description = "A test description",
                Tags = new List<string> { "test" },
                UserID = "student-003",
                UserName = "Student Three"
            };
            _controller.ModelState.AddModelError("Title", "Title is required");

            // Act
            var result = await _controller.CreateCourse(newCourse);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateCourse_MissingUserID_ReturnsBadRequest()
        {
            // Arrange
            var newCourse = new CreateCourseDTO
            {
                Title = "Test Course",
                Description = "A test description",
                Tags = new List<string> { "test" },
                UserID = null,
                UserName = "Student One"
            };
            _controller.ModelState.AddModelError("UserID", "UserID is required");

            // Act
            var result = await _controller.CreateCourse(newCourse);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateCourse_WrongUser_ReturnsForbid()
        {
            // Arrange: token user is student-003, but DTO uses student-001
            var newCourse = new CreateCourseDTO
            {
                Title = "Mismatch",
                Description = "Should be forbidden",
                Tags = new List<string> { "x" },
                UserID = "student-001",
                UserName = "Student One"
            };

            // Act
            var result = await _controller.CreateCourse(newCourse);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task UpdateCourse_ValidData_ReturnsNoContent()
        {
            // Arrange: pick a course created by student-003
            var course = _context.Courses.FirstOrDefault(c => c.UserID == "student-003");
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Tags = new List<string> { "updated" }
            };

            // Act
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var updatedCourse = await _context.Courses.FindAsync(course.CourseID);
            Assert.Equal("Updated Title", updatedCourse.Title);
        }

        [Fact]
        public async Task UpdateCourse_NonExistentId_ReturnsNotFound()
        {
            // Arrange
            var updateDto = new UpdateCourseDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Tags = new List<string> { "updated" }
            };

            // Act
            var result = await _controller.UpdateCourse(-1, updateDto);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateCourse_InvalidTitle_ReturnsBadRequest()
        {
            // Arrange
            var course = _context.Courses.FirstOrDefault(c => c.UserID == "student-003");
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "",
                Description = "Updated Description",
                Tags = new List<string> { "updated" }
            };

            // Act
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Title cannot be empty", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateCourse_InvalidDescription_ReturnsBadRequest()
        {
            // Arrange
            var course = _context.Courses.FirstOrDefault(c => c.UserID == "student-003");
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "Updated Title",
                Description = "",
                Tags = new List<string> { "updated" }
            };

            // Act
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Description cannot be empty", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateCourse_WrongUser_ReturnsForbid()
        {
            // Arrange: pick a course created by someone else (e.g. student-002)
            var course = _context.Courses.FirstOrDefault(c => c.UserID != "student-003");
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "Attempted Update",
                Description = "Should be forbidden",
                Tags = new List<string> { "x" }
            };

            // Act
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== ENROLL Tests ====================

        [Fact]
        public async Task EnrollStudent_ValidIds_ReturnsNoContent()
        {
            // Arrange: Choose a course and a student not already enrolled.
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            await _context.Entry(course).Collection(c => c.EnrolledStudents).LoadAsync();
            string studentId = "student-003";
            var student = await _context.Students.FindAsync(studentId);
            Assert.NotNull(student);

            if (course.EnrolledStudents.Any(s => s.UserID == studentId))
            {
                course.EnrolledStudents.Remove(student);
                await _context.SaveChangesAsync();
            }

            // Act
            var result = await _controller.EnrollStudent(course.CourseID, studentId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _context.ChangeTracker.Clear();
            var updatedCourse = await _context.Courses
                .Include(c => c.EnrolledStudents)
                .FirstAsync(c => c.CourseID == course.CourseID);
            Assert.Contains(updatedCourse.EnrolledStudents, s => s.UserID == studentId);
        }

        [Fact]
        public async Task EnrollStudent_AlreadyEnrolled_ReturnsNoContent()
        {
            // Arrange
            var course = _context.Courses.First(c => c.EnrolledStudents.Any());
            Assert.NotNull(course);
            var student = course.EnrolledStudents.First();
            Assert.NotNull(student);
            int initialCount = course.EnrolledStudents.Count;

            // Act
            var result = await _controller.EnrollStudent(course.CourseID, student.UserID);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var reloaded = await _context.Courses.FindAsync(course.CourseID);
            Assert.Equal(initialCount, reloaded.EnrolledStudents.Count);
        }

        [Fact]
        public async Task EnrollStudent_NonExistentCourse_ReturnsNotFound()
        {
            // Act
            var result = await _controller.EnrollStudent(-1, "student-003");

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Course or Student not found", notFound.Value);
        }

        // ==================== UNENROLL Tests ====================

        [Fact]
        public async Task UnenrollStudent_ValidIds_ReturnsNoContent()
        {
            // Arrange
            var course = _context.Courses.First(c => c.EnrolledStudents.Any());
            Assert.NotNull(course);
            var student = course.EnrolledStudents.First();

            // Act
            var result = await _controller.UnenrollStudent(course.CourseID, student.UserID);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var updatedCourse = await _context.Courses
                .Include(c => c.EnrolledStudents)
                .FirstAsync(c => c.CourseID == course.CourseID);
            Assert.DoesNotContain(updatedCourse.EnrolledStudents, s => s.UserID == student.UserID);
        }

        [Fact]
        public async Task UnenrollStudent_StudentNotEnrolled_ReturnsBadRequest()
        {
            // Arrange
            var course = _context.Courses.First(c => c.CourseID == 1);
            Assert.NotNull(course);

            // Act
            var result = await _controller.UnenrollStudent(course.CourseID, "student-003");

            // Assert
            var badReq = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Student is not enrolled in this course", badReq.Value);
        }

        [Fact]
        public async Task UnenrollStudent_NonExistentCourse_ReturnsNotFound()
        {
            // Act
            var result = await _controller.UnenrollStudent(-1, "student-003");

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Course not found", notFound.Value);
        }

        [Fact]
        public async Task UnenrollStudent_NonExistentStudent_ReturnsForbidden()
        {
            // Arrange
            var course = _context.Courses.First();
            Assert.NotNull(course);

            // Act
            var result = await _controller.UnenrollStudent(course.CourseID, "no-one");

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== DELETE Tests ====================

        [Fact]
        public async Task DeleteCourse_NonExistentId_ReturnsNotFound()
        {
            // Act
            var result = await _controller.DeleteCourse(-1);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteCourse_WithEnrolledStudents_ReturnsNoContent()
        {
            // Arrange
            var course = _context.Courses.First(c => c.EnrolledStudents.Any());
            Assert.NotNull(course);
            var enrolledStudents = course.EnrolledStudents.ToList();

            // Act
            var result = await _controller.DeleteCourse(course.CourseID);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var deleted = await _context.Courses.FindAsync(course.CourseID);
            Assert.Null(deleted);
            foreach (var s in enrolledStudents)
                Assert.NotNull(await _context.Students.FindAsync(s.UserID));
        }
    }
}
