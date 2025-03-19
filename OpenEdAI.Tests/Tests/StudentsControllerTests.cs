using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;
using OpenEdAI.Tests.TestHelpers;
using Xunit;

namespace OpenEdAI.Tests.Tests
{
    public class StudentsControllerTests : BaseTest
    {
        private readonly StudentsController _controller;
        public StudentsControllerTests() : base()
        {
            // Initialize the controller with the in-memory database context
            _controller = new StudentsController(_context);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = GetMockUser()
                }
            };
        }

        // ==================== GET Tests ====================

        [Fact]
        public async Task GetStudents_ReturnsListOfStudents()
        {
            // Act: Retrieve all students
            var result = await _controller.GetStudents();

            // Assert: Check if the result OK and is a list of students
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var students = Assert.IsType<List<Student>>(okResult.Value);
            Assert.NotEmpty(students);
        }

        [Fact]
        public async Task GetStudent_ValidId_ReturnsStudent()
        {
            // Arrange: Get an existing student from the database
            var student = _context.Students.FirstOrDefault();
            Assert.NotNull(student);

            // Act: Retrieve the student by ID
            var result = await _controller.GetStudent(student.UserID);

            // Assert: Verfiy the correct student is returned
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedStudent = Assert.IsType<StudentDTO>(okResult.Value);
            Assert.Equal(student.UserID, returnedStudent.UserID);
        }

        [Fact]
        public async Task GetStudent_InvalidId_ReturnsNotFound()
        {
            // Act: Attempt to get a student with an invalid ID
            var result = await _controller.GetStudent("invalid-id");

            // Assert: Verifty the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", notFoundResult.Value);
        }

        [Fact]
        public async Task GetCreatedCourses_ValidId_ReturnsCreatedCourses()
        {
            // Arrange: Get an existing student with created courses
            var student = _context.Students.Include(s => s.CreatorCourses).FirstOrDefault(s => s.CreatorCourses.Any());
            Assert.NotNull(student);

            // Act: Retrieve the student's courses
            var result = await _controller.GetCreatedCourses(student.UserID);

            // Assert: Verify that the response is OK and contains a list of courses
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var courses = Assert.IsAssignableFrom<IEnumerable<CourseDTO>>(okResult.Value);
            Assert.NotEmpty(courses);
        }

        [Fact]
        public async Task GetCreatedCourses_InvalidId_ReturnsNotFound()
        {
            // Act: Attempt to get created courses with an invalid student ID
            var result = await _controller.GetCreatedCourses("invalid-id");

            // Assert: Verify the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", notFoundResult.Value);
        }

        [Fact]
        public async Task GetCreatedCourses_NoCreatedCourses_ReturnsNotFound()
        {
            // Arrange: Get an existing student with no created courses
            var student = _context.Students.FirstOrDefault(s => s.UserID == "00000000-0000-0000-0000-000000000000");
            Assert.NotNull(student);

            // Act: Attempt to get created courses for a student with no created courses
            var result = await _controller.GetCreatedCourses(student.UserID);

            // Assert: Verify the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetEnrolledCourses_ValidId_ReturnsCourses()
        {
            // Arrange: Get an existing student with enrolled courses
            var student = _context.Students.Include(s => s.EnrolledCourses).FirstOrDefault(s => s.EnrolledCourses.Any());
            Assert.NotNull(student);

            // Act: Retrieve the student's enrolled courses
            var result = await _controller.GetEnrolledCourses(student.UserID);

            // Assert: Verify that the response is OK and contains a list of courses
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var courses = Assert.IsAssignableFrom<IEnumerable<CourseDTO>>(okResult.Value);
            Assert.NotEmpty(courses);
        }

        [Fact]
        public async Task GetEnrolledCourses_InvalidId_ReturnsNotFound()
        {
            // Act: Attempt to get enrolled courses with an invalid student ID
            var result = await _controller.GetEnrolledCourses("invalid-id");

            // Assert: Verify the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", notFoundResult.Value);
        }

        [Fact]
        public async Task GetEnrolledCourses_NoEnrolledCourses_ReturnsNotFound()
        {
            // Arrange: Get an existing student with no enrolled courses
            var student = _context.Students.FirstOrDefault(s => s.UserID == "00000000-0000-0000-0000-000000000000");
            Assert.NotNull(student);

            // Act: Attempt to get enrolled courses for a student with no enrolled courses
            var result = await _controller.GetEnrolledCourses(student.UserID);

            // Assert: Verify the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetStudentProgress_ValidId_ReturnsProgressRecords()
        {
            // Arrange: Get an existing student with progress records
            var student = _context.Students.Include(s => s.ProgressRecords).FirstOrDefault(s => s.ProgressRecords.Any());
            Assert.NotNull(student);

            // Act: Retrieve the student's progress records
            var result = await _controller.GetStudentProgress(student.UserID);

            // Assert: Verify that the response is OK and contains a list of progress records
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var progressRecords = Assert.IsAssignableFrom<IEnumerable<CourseProgressDTO>>(okResult.Value);
            Assert.NotEmpty(progressRecords);
        }

        [Fact]
        public async Task GetStudentProgress_InvalidId_ReturnsNotFound()
        {
            // Act: Attempt to get progress records with an invalid student ID
            var result = await _controller.GetStudentProgress("invalid-id");

            // Assert: Verify the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", notFoundResult.Value);
        }

        [Fact]
        public async Task GetStudentProgress_ProgressRecords_ReturnsNotFound()
        {
            // Arrange: Get an existing student with no progress records
            var student = _context.Students.FirstOrDefault(s => s.UserID == "00000000-0000-0000-0000-000000000000");
            Assert.NotNull(student);

            // Act: Attempt to get progress records for a student with no progress records
            var result = await _controller.GetStudentProgress(student.UserID);

            // Assert: Verify the response is NotFound
            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateStudent_ValidData_ReturnsCreatedStudent()
        {
            // Arrange: Create a new student DTO
            var newStudent = new StudentDTO
            {
                UserID = "student-004",
                Username = "Student Four"
            };

            // Act: Create the student
            var result = await _controller.CreateStudent(newStudent);

            // Assert: Verify the response is Created and the student is returned
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var studentDto = Assert.IsType<StudentDTO>(createdResult.Value);
            Assert.Equal(newStudent.UserID, studentDto.UserID);
        }

        [Fact]
        public async Task CreateStudent_ExistingStudent_ReturnsConflict()
        {
            // Arrange: retrieve an existing student
            var student = _context.Students.FirstOrDefault();
            Assert.NotNull(student);

            // Create a new student DTO with the same ID
            var createDto = new StudentDTO
            {
                UserID = student.UserID,
                Username = student.UserName,
            };

            // Act: Attempt to create a duplicate student
            var result = await _controller.CreateStudent(createDto);

            // Assert: Verify the response is Conflict
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal("Student already exists", conflictResult.Value);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task UpdateStudent_ValidId_ReturnsNoContent()
        {
            // Arrange: Get the mock student by id
            var student = _context.Students.Find("student-003");
            Assert.NotNull(student);
            var updateDto = new UpdateStudentDTO
            {
                Username = "Updated Student",
            };

            // Act: Attempt to update the student
            var result = await _controller.UpdateStudent(student.UserID, updateDto);

            // Assert: Verify the response is NoContent and that the student was updated
            Assert.IsType<NoContentResult>(result);
            var updatedStudent = await _context.Students.FindAsync(student.UserID);
            Assert.NotNull(updatedStudent);
            Assert.Equal(updateDto.Username, updatedStudent.UserName);
        }

        [Fact]
        public async Task UpdateStudent_InvalidId_ReturnsForbidden()
        {
            // Arrange: Prepare an update DTO
            var updateDto = new UpdateStudentDTO
            {
                Username = "Updated Student"
            };

            // Act: Attempt to update a non-existing student
            var result = await _controller.UpdateStudent("invalid-id", updateDto);

            // Assert: Verify the response is ForbidResult
            var forbidResult = Assert.IsType<ForbidResult>(result);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task DeleteStudent_ValidId_DeletesRelatedData()
        {
            // Arrange: Get an existing student with progress records and enrollments
            var student = _context.Students
                .Include(s => s.ProgressRecords)
                .Include(s => s.EnrolledCourses)
                .FirstOrDefault(s => s.ProgressRecords.Any() && s.EnrolledCourses.Any());
            Assert.NotNull(student);

            var studentId = student.UserID;

            // Act: Delete the student
            var result = await _controller.DeleteStudent(studentId);

            // Assert: Ensure the response is NoContent
            Assert.IsType<NoContentResult>(result);

            // Assert: Verify the student is removed
            var deletedStudent = await _context.Students.FindAsync(studentId);
            Assert.Null(deletedStudent);

            // Assert: Verify related progress records are removed
            var deletedProgressRecords = _context.CourseProgress.Where(p => p.UserID == studentId).ToList();
            Assert.Empty(deletedProgressRecords);

            // Assert: Verify related enrollments are removed
            var remainingEnrollments = _context.Courses
                .Where(c => c.EnrolledStudents.Any(s => s.UserID == studentId))
                .ToList();
            Assert.Empty(remainingEnrollments);

        }

        [Fact]
        public async Task DeleteStudent_ValidId_ReassignsCreatedCourses()
        {
            // Arrange: Get an existing student with created courses
            var student = _context.Students
                .Include(s => s.CreatorCourses)
                .FirstOrDefault(s => s.UserID == "student-003" && s.CreatorCourses.Any());
            Assert.NotNull(student);

            var studentId = student.UserID;
            var createdCourses = student.CreatorCourses.ToList();
            string dummyUserId = "00000000-0000-0000-0000-000000000000";

            // Act: Delete the student
            var result = await _controller.DeleteStudent(studentId);

            // Assert: Ensure response is NoContent
            Assert.IsType<NoContentResult>(result);

            // Assert: Verify created courses are rassigned to `Deleted User` (dummy user)
            foreach (var course in createdCourses)
            {
                var updatedCourse = await _context.Courses.FindAsync(course.CourseID);
                Assert.NotNull(updatedCourse);
                Assert.Equal(dummyUserId, updatedCourse.UserID);
            }
        }

        [Fact]
        public async Task DeleteStudent_InvalidId_ReturnsForbidden()
        {
            // Act: Attempt to delete a non-existing student
            var result = await _controller.DeleteStudent("invalid-id");

            // Assert: Verify the response is ForbidResult
            var forbidResult = Assert.IsType<ForbidResult>(result);
        }

        
    }
}
