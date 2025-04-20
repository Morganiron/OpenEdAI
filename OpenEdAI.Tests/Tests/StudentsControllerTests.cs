using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Controllers;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;
using OpenEdAI.Tests.TestHelpers;

namespace OpenEdAI.Tests.Tests
{
    public class StudentsControllerTests : BaseTest
    {
        private readonly StudentsController _controller;

        public StudentsControllerTests() : base()
        {
            // Initialize controller with in‑memory context and default mock user student-003
            _controller = new StudentsController(_context);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = GetMockUser("student-003", "Student Three")
                }
            };
        }

        // ==================== GET Tests ====================

        [Fact]
        public async Task GetStudents_ReturnsListOfStudents()
        {
            // Act: call endpoint to retrieve all students
            var result = await _controller.GetStudents();

            // Assert: should be OK and return a non-empty list of Student entities
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var students = Assert.IsType<List<Student>>(ok.Value);
            Assert.NotEmpty(students);
        }

        [Fact]
        public async Task GetStudent_ValidId_ReturnsStudent()
        {
            // Arrange: pick an existing student from seeded data
            var existing = _context.Students.First();

            // Act: retrieve by that student's UserID
            var result = await _controller.GetStudent(existing.UserID);

            // Assert: should be OK and return a StudentDTO with matching UserID
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<StudentDTO>(ok.Value);
            Assert.Equal(existing.UserID, dto.UserID);
        }

        [Fact]
        public async Task GetStudent_InvalidId_ReturnsNotFound()
        {
            // Act: attempt to retrieve a non-existent student
            var result = await _controller.GetStudent("no-such-user");

            // Assert: should be NotFound with the expected message
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", nf.Value);
        }

        [Fact]
        public async Task GetCreatedCourses_ValidId_ReturnsCreatedCourses()
        {
            // Arrange: choose a student who has created courses
            var student = _context.Students
                .Include(s => s.CreatorCourses)
                .First(s => s.CreatorCourses.Any());

            // Act: get the courses this student created
            var result = await _controller.GetCreatedCourses(student.UserID);

            // Assert: OK and list contains at least one CourseDTO
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var courses = Assert.IsAssignableFrom<IEnumerable<CourseDTO>>(ok.Value);
            Assert.NotEmpty(courses);
        }

        [Fact]
        public async Task GetCreatedCourses_InvalidId_ReturnsNotFound()
        {
            // Act: call with a userId that doesn't exist
            var result = await _controller.GetCreatedCourses("no-such-user");

            // Assert: NotFound with the expected message
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", nf.Value);
        }

        [Fact]
        public async Task GetCreatedCourses_NoCreatedCourses_ReturnsNotFound()
        {
            // Arrange: pick the dummy "Deleted User" who has no created courses
            var student = _context.Students
                .First(s => s.UserID == "00000000-0000-0000-0000-000000000000");

            // Act: attempt to get their created courses
            var result = await _controller.GetCreatedCourses(student.UserID);

            // Assert: NotFound (empty collection)
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetEnrolledCourses_ValidId_ReturnsCourses()
        {
            // Arrange: pick a student who is enrolled in at least one course
            var student = _context.Students
                .Include(s => s.EnrolledCourses)
                .First(s => s.EnrolledCourses.Any());

            // Act: retrieve their enrolled courses
            var result = await _controller.GetEnrolledCourses(student.UserID);

            // Assert: OK and non-empty list of CourseDTO
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var courses = Assert.IsAssignableFrom<IEnumerable<CourseDTO>>(ok.Value);
            Assert.NotEmpty(courses);
        }

        [Fact]
        public async Task GetEnrolledCourses_InvalidId_ReturnsNotFound()
        {
            // Act: call with invalid userId
            var result = await _controller.GetEnrolledCourses("no-such-user");

            // Assert: NotFound with message
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", nf.Value);
        }

        [Fact]
        public async Task GetEnrolledCourses_NoEnrolledCourses_ReturnsNotFound()
        {
            // Arrange: pick dummy user who has no enrollments
            var student = _context.Students
                .First(s => s.UserID == "00000000-0000-0000-0000-000000000000");

            // Act
            var result = await _controller.GetEnrolledCourses(student.UserID);

            // Assert: NotFound with the “No enrolled courses” message
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("No enrolled courses", nf.Value);
        }

        [Fact]
        public async Task GetStudentProgress_ValidId_ReturnsProgressRecords()
        {
            // Arrange: pick a student with existing progress records
            var student = _context.Students
                .Include(s => s.ProgressRecords)
                .First(s => s.ProgressRecords.Any());

            // Act: retrieve their progress
            var result = await _controller.GetStudentProgress(student.UserID);

            // Assert: OK and non-empty list of CourseProgressDTO
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var records = Assert.IsAssignableFrom<IEnumerable<CourseProgressDTO>>(ok.Value);
            Assert.NotEmpty(records);
        }

        [Fact]
        public async Task GetStudentProgress_InvalidId_ReturnsNotFound()
        {
            // Act: call with invalid userId
            var result = await _controller.GetStudentProgress("no-such-user");

            // Assert: NotFound with “Student not found”
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Student not found", nf.Value);
        }

        [Fact]
        public async Task GetStudentProgress_NoProgressRecords_ReturnsNotFound()
        {
            // Arrange: dummy user with no progress
            var student = _context.Students
                .First(s => s.UserID == "00000000-0000-0000-0000-000000000000");

            // Act
            var result = await _controller.GetStudentProgress(student.UserID);

            // Assert: NotFound (empty progress)
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // ==================== CREATE Tests ====================

        [Fact]
        public async Task CreateStudent_ValidData_ReturnsCreatedStudent()
        {
            // Arrange: swap in a mock user that doesn’t yet exist
            _controller.ControllerContext.HttpContext.User =
                GetMockUser("student-004", "Student Four");

            // Act: call CreateStudent
            var result = await _controller.CreateStudent();

            // Assert: CreatedAtAction with correct DTO values
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<StudentDTO>(created.Value);
            Assert.Equal("student-004", dto.UserID);
            Assert.Equal("Student Four", dto.Username);
        }

        [Fact]
        public async Task CreateStudent_ExistingStudent_ReturnsConflict()
        {
            // Arrange: default mock user student-003 already exists
            _controller.ControllerContext.HttpContext.User =
                GetMockUser("student-003", "Student Three");

            // Act
            var result = await _controller.CreateStudent();

            // Assert: Conflict with message
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal("Student already exists", conflict.Value);
        }

        [Fact]
        public async Task CreateStudent_NoToken_ReturnsUnauthorized()
        {
            // Arrange: no claims present
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();

            // Act
            var result = await _controller.CreateStudent();

            // Assert: Unauthorized with error about missing ID or username
            var unAuth = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Invalid token. User ID or username not found.", unAuth.Value);
        }

        // ==================== UPDATE Tests ====================

        [Fact]
        public async Task UpdateStudent_ValidId_ReturnsNoContent()
        {
            // Arrange: update own profile
            var dto = new UpdateStudentDTO { Username = "Updated Name" };

            // Act
            var result = await _controller.UpdateStudent("student-003", dto);

            // Assert: NoContent and actual name change in DB
            Assert.IsType<NoContentResult>(result);
            var updated = await _context.Students.FindAsync("student-003");
            Assert.Equal("Updated Name", updated.UserName);
        }

        [Fact]
        public async Task UpdateStudent_InvalidId_ReturnsForbid()
        {
            // Arrange: attempt to update someone else’s profile
            var dto = new UpdateStudentDTO { Username = "Should Not Succeed" };

            // Act
            var result = await _controller.UpdateStudent("student-002", dto);

            // Assert: non-admin cannot update another user → Forbid
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== COMPLETE SETUP Tests ====================

        [Fact]
        public async Task CompleteSetup_ValidId_MarksSetupComplete()
        {
            // Act: mark own setup complete
            var result = await _controller.CompleteSetup("student-003");

            // Assert: NoContent and flag set in DB
            Assert.IsType<NoContentResult>(result);
            var student = await _context.Students.FindAsync("student-003");
            Assert.True(student.HasCompletedSetup);
        }

        [Fact]
        public async Task CompleteSetup_NotOwner_ReturnsForbid()
        {
            // Act: try to mark another student’s setup
            var result = await _controller.CompleteSetup("student-002");

            // Assert: should be forbidden
            Assert.IsType<ForbidResult>(result);
        }

        // ==================== DELETE Tests ====================

        [Fact]
        public async Task DeleteStudent_ValidId_DeletesRelatedData()
        {
            // Arrange: pick a student with both progress and enrollments
            var student = _context.Students
                .Include(s => s.ProgressRecords)
                .Include(s => s.EnrolledCourses)
                .First(s => s.ProgressRecords.Any() && s.EnrolledCourses.Any());

            // Act: delete that student
            var result = await _controller.DeleteStudent(student.UserID);

            // Assert: NoContent; student, their progress & enrollments removed
            Assert.IsType<NoContentResult>(result);
            Assert.Null(await _context.Students.FindAsync(student.UserID));
            Assert.Empty(_context.CourseProgress.Where(p => p.UserID == student.UserID));
            Assert.Empty(_context.Courses.Where(c => c.EnrolledStudents.Any(s => s.UserID == student.UserID)));
        }

        [Fact]
        public async Task DeleteStudent_ValidId_ReassignsCreatedCourses()
        {
            // Arrange: pick student-003 who has created courses
            var student = _context.Students
                .Include(s => s.CreatorCourses)
                .First(s => s.UserID == "student-003" && s.CreatorCourses.Any());
            var created = student.CreatorCourses.ToList();

            // Act: delete that student
            var result = await _controller.DeleteStudent(student.UserID);

            // Assert: NoContent; all their courses now point at dummy user
            Assert.IsType<NoContentResult>(result);
            const string dummy = "00000000-0000-0000-0000-000000000000";
            foreach (var c in created)
            {
                var updated = await _context.Courses.FindAsync(c.CourseID);
                Assert.Equal(dummy, updated.UserID);
            }
        }

        [Fact]
        public async Task DeleteStudent_InvalidId_ReturnsForbid()
        {
            // Act: try to delete another user (non-admin)
            var result = await _controller.DeleteStudent("no-such-user");

            // Assert: forbidden
            Assert.IsType<ForbidResult>(result);
        }
    }
}
