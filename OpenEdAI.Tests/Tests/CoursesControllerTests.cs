using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Models;
using OpenEdAI.API.Data;
using OpenEdAI.Tests.TestHelpers;
using Xunit;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Controllers;

namespace OpenEdAI.Tests.Tests
{
    public class CoursesControllerTests : BaseTest
    {
        private readonly CoursesController _controller;

        public CoursesControllerTests() : base()
        {
            // Initialize the controller with the DbContext and and mock user
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

            // Assert: Verify that the result is OK and returns a non-empty collection.
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var courses = Assert.IsAssignableFrom<IEnumerable<CourseDTO>>(okResult.Value);
            Assert.NotEmpty(courses);
        }

        [Fact]
        public async Task GetCourse_ValidId_ReturnsCourse()
        {
            // Arrange: Get an existing course from the seeded data.
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            // Act: Retrieve the course by ID.
            var result = await _controller.GetCourse(course.CourseID);

            // Assert: Verify that the returned course matches the expected course.
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedCourse = Assert.IsType<CourseDTO>(okResult.Value);
            Assert.Equal(course.CourseID, returnedCourse.CourseID);
        }

        [Fact]
        public async Task GetCourse_InvalidId_ReturnsNotFound()
        {
            // Act: Try to retrieve a course using an invalid ID.
            var result = await _controller.GetCourse(-1);

            // Assert: Expect a NotFound response.
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateCourse_ReturnsCreatedCourse()
        {
            // Arrange: Create a valid CreateCourseDTO.
            var newCourse = new CreateCourseDTO
            {
                Title = "Test Course",
                Description = "A test description",
                Tags = new List<string> { "test", "csharp" },
                UserID = "student-003",
                UserName = "Student Three"
            };

            // Act: Create the course.
            var result = await _controller.CreateCourse(newCourse);

            // Assert: Verify that the course is created and the returned data is correct.
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdCourse = Assert.IsType<CourseDTO>(createdResult.Value);
            Assert.Equal(newCourse.Title, createdCourse.Title);
        }

        [Fact]
        public async Task CreateCourse_MissingTitle_ReturnsBadRequest()
        {
            // Arrange: Simulate missing title by adding a model state error.
            var newCourse = new CreateCourseDTO
            {
                Title = null,
                Description = "A test description",
                Tags = new List<string> { "test" },
                UserID = "student-003", 
                UserName = "Student Three"
            };
            _controller.ModelState.AddModelError("Title", "Title is required");

            // Act: Attempt to create the course.
            var result = await _controller.CreateCourse(newCourse);

            // Assert: Expect a BadRequest response.
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateCourse_MissingUserID_ReturnsBadRequest()
        {
            // Arrange: Simulate missing UserID via model state error.
            var newCourse = new CreateCourseDTO
            {
                Title = "Test Course",
                Description = "A test description",
                Tags = new List<string> { "test" },
                UserID = null,
                UserName = "Student One"
            };
            _controller.ModelState.AddModelError("UserID", "UserID is required");

            // Act: Attempt to create the course.
            var result = await _controller.CreateCourse(newCourse);

            // Assert: Expect a BadRequest response.
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task UpdateCourse_ValidData_ReturnsNoContent()
        {
            // Arrange: Pick an existing course and prepare a valid update.
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Tags = new List<string> { "updated" }
            };

            // Act: Update the course.
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert: Verify NoContent is returned and the course is updated.
            Assert.IsType<NoContentResult>(result);
            var updatedCourse = await _context.Courses.FindAsync(course.CourseID);
            Assert.Equal("Updated Title", updatedCourse.Title);
        }

        [Fact]
        public async Task UpdateCourse_NonExistentId_ReturnsNotFound()
        {
            // Arrange: Prepare a valid update DTO.
            var updateDto = new UpdateCourseDTO
            {
                Title = "Updated Title",
                Description = "Updated Description",
                Tags = new List<string> { "updated" }
            };

            // Act: Attempt to update a course with an invalid ID.
            var result = await _controller.UpdateCourse(-1, updateDto);

            // Assert: Expect a NotFound response.
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateCourse_InvalidTitle_ReturnsBadRequest()
        {
            // Arrange: Simulate invalid data (empty title)
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "", // Empty title is invalid.
                Description = "Updated Description",
                Tags = new List<string> { "updated" }
            };
            

            // Act: Attempt to update the course.
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert: Expect a BadRequest response.
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Title cannot be empty", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateCourse_InvalidDescription_ReturnsBadRequest()
        {
            // Arrange: Simulate invalid data (empty title)
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);
            var updateDto = new UpdateCourseDTO
            {
                Title = "Updated Title",
                Description = "",
                Tags = new List<string> { "updated" }
            };


            // Act: Attempt to update the course.
            var result = await _controller.UpdateCourse(course.CourseID, updateDto);

            // Assert: Expect a BadRequest response.
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Description cannot be empty", badRequestResult.Value);
        }



        // ==================== ENROLL Tests ====================

        [Fact]
        public async Task EnrollStudent_ValidIds_ReturnsNoContent()
        {
            // Arrange: Choose a course and a student not already enrolled.
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);

            // Explicitly load the EnrolledStudents navigation property
            await _context.Entry(course).Collection(c => c.EnrolledStudents).LoadAsync();

            string studentId = "student-003";

            // Ensure the student exists.
            var student = await _context.Students.FindAsync(studentId);
            Assert.NotNull(student);

            // Remove the student if already enrolled to ensure test isolation.
            if (course.EnrolledStudents.Any(s => s.UserID == studentId))
            {
                course.EnrolledStudents.Remove(student);
                await _context.SaveChangesAsync();
            }

            // Act: Enroll the student.
            var result = await _controller.EnrollStudent(course.CourseID, studentId);

            // Assert: Verify enrollment.
            Assert.IsType<NoContentResult>(result);

            // Clear the change tracker to get a fresh query
            _context.ChangeTracker.Clear();
            var updatedCourse = await _context.Courses
                .Include(c => c.EnrolledStudents)
                .FirstOrDefaultAsync(c => c.CourseID == course.CourseID);

            Assert.Contains(updatedCourse.EnrolledStudents, s => s.UserID == studentId);
        }

        [Fact]
        public async Task EnrollStudent_AlreadyEnrolled_ReturnsNoContent()
        {
            // Arrange: Choose a course and a student that is already enrolled.
            var course = _context.Courses.FirstOrDefault(c => c.EnrolledStudents.Any());
            Assert.NotNull(course);

            var student = course.EnrolledStudents.FirstOrDefault();
            Assert.NotNull(student);

            int initialCount = course.EnrolledStudents.Count;

            // Act: Attempt to enroll the same student again.
            var result = await _controller.EnrollStudent(course.CourseID, student.UserID);

            // Assert: Verify that the enrollment is did not happen.
            Assert.IsType<NoContentResult>(result);
            var updatedCourse = await _context.Courses.FindAsync(course.CourseID);
            Assert.Equal(initialCount, updatedCourse.EnrolledStudents.Count);
        }

        [Fact]
        public async Task EnrollStudent_NonExistentCourse_ReturnsNotFound()
        {
            // Act: Attempt to enroll a student in a non-existent course.
            var result = await _controller.EnrollStudent(-1, "student-003");

            // Assert: Expect a NotFound response.
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Course or Student not found", notFoundResult.Value);
        }

        // ==================== UNENROLL Tests ====================

        [Fact]
        public async Task UnenrollStudent_ValidIds_ReturnsNoContent()
        {
            // Arrange: Choose a course that has enrolled students.
            var course = _context.Courses.FirstOrDefault(c => c.EnrolledStudents.Any());
            Assert.NotNull(course);
            var student = course.EnrolledStudents.First();
            Assert.NotNull(student);

            // Act: Unenroll the student.
            var result = await _controller.UnenrollStudent(course.CourseID, student.UserID);

            // Assert: Verify that the student is removed.
            Assert.IsType<NoContentResult>(result);
            var updatedCourse = await _context.Courses
                .Include(c => c.EnrolledStudents)
                .FirstOrDefaultAsync(c => c.CourseID == course.CourseID);
            Assert.DoesNotContain(updatedCourse.EnrolledStudents, s => s.UserID == student.UserID);
        }

        [Fact]
        public async Task UnenrollStudent_StudentNotEnrolled_ReturnsBadRequest()
        {
            // Arrange: Choose a course and a student that is not enrolled.

            // Use "student-003" that is not enrolled in the first course (courseId = 1)
            string studentId = "student-003";

            // Get course where the CourseID == 1
            var course = _context.Courses
                .Where(c => c.CourseID == 1)
                .FirstOrDefault();

            Assert.NotNull(course);
            Console.WriteLine($"\n\n------\nUnenrollStudent_StudentNotEnrolled_ReturnsBadRequest - Selected CourseID: {course.CourseID}");
            
            // Act: Attempt to unenroll the non-enrolled student.
            var result = await _controller.UnenrollStudent(course.CourseID, studentId);
            

            // Assert: Expect a BadRequest response.
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Student is not enrolled in this course", badRequestResult.Value);
        }

        [Fact]
        public async Task UnenrollStudent_NonExistentCourse_ReturnsNotFound()
        {
            // Act: Attempt to unenroll a student from a non-existent course.
            var result = await _controller.UnenrollStudent(-1, "student-003");

            // Assert: Expect a NotFound response.
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Course not found", notFoundResult.Value);
        }

        [Fact]
        public async Task UnenrollStudent_NonExistentStudent_ReturnsForbidden()
        {
            // Arrange: Choose an existing course and a student ID that does not exist.
            var course = _context.Courses.FirstOrDefault();
            Assert.NotNull(course);
            var result = await _controller.UnenrollStudent(course.CourseID, "non-existent-student");

            // Assert: Expect a ForbidResult response since studentId does not match
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== DELETE Tests ====================

        [Fact]
        public async Task DeleteCourse_NonExistentId_ReturnsNotFound()
        {
            // Act: Attempt to delete a course using an invalid ID.
            var result = await _controller.DeleteCourse(-1);

            // Assert: Expect a NotFound response.
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteCourse_WithEnrolledStudents_ReturnsNoContent()
        {
            // Arrange: Pick a course that has enrolled students.
            var course = _context.Courses.FirstOrDefault(c => c.EnrolledStudents.Any());
            Assert.NotNull(course);
            var enrolledStudents = course.EnrolledStudents.ToList();

            // Act: Delete the course.
            var result = await _controller.DeleteCourse(course.CourseID);

            // Assert: Verify that deletion returns NoContent and the course is removed,
            // while enrolled student records remain intact.
            Assert.IsType<NoContentResult>(result);
            var deletedCourse = await _context.Courses.FindAsync(course.CourseID);
            Assert.Null(deletedCourse);
            foreach (var student in enrolledStudents)
            {
                Assert.NotNull(await _context.Students.FindAsync(student.UserID));
            }
        }
    }
}
