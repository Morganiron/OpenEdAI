using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Filters;
using OpenEdAI.API.Models;

namespace OpenEdAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : BaseController
    {
        private ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Students
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudents()
        {
            var students = await _context.Students.ToListAsync();
            return Ok(students);
        }

        // GET: api/Students/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<Student>> GetStudent(string userId)
        {
            var studentDTO = await _context.Students
                .Where(s => s.UserID == userId)
                .Select(s => new StudentDTO
                {
                    UserID = s.UserID,
                    Username = s.UserName,
                    HasCompletedSetup = s.HasCompletedSetup,
                    Profile = s.Profile == null ? null : new StudentProfileDTO
                    {
                        EducationLevel = s.Profile.EducationLevel,
                        PreferredContentTypes = s.Profile.PreferredContentTypes,
                        SpecialConsiderations = s.Profile.SpecialConsiderations,
                        AdditionalConsiderations = s.Profile.AdditionalConsiderations
                    },
                    CreatorCourseIds = s.CreatorCourses.Select(c => c.CourseID).ToList(),
                    EnrolledCourseIds = s.EnrolledCourses.Select(c => c.CourseID).ToList(),
                    ProgressRecordIds = s.ProgressRecords.Select(p => p.ProgressID).ToList()
                })
                .FirstOrDefaultAsync();
                

            if (studentDTO == null)
                return NotFound("Student not found");

            return Ok(studentDTO);
        }

        // GET: api/Students/{userId}/CreatorCourses - Get all courses that a student has created
        [HttpGet("{userId}/CreatorCourses")]
        public async Task<ActionResult<IEnumerable<CourseDTO>>> GetCreatedCourses(string userId)
        {
            // Check if the student exists
            var student = await _context.Students.FindAsync(userId);

            if (student == null)
            {
                return NotFound("Student not found");
            }

            var courses = await _context.Students
                .Where(s => s.UserID == userId)
                .SelectMany(s => s.CreatorCourses)
                .Select(c => new CourseDTO
                {
                    CourseID = c.CourseID,
                    Title = c.Title,
                    Description = c.Description,
                    Tags = c.Tags,
                    UserID = c.UserID,
                    UserName = c.UserName,
                    CreatedDate = c.CreatedDate,
                    UpdateDate = c.UpdateDate,
                    LessonIds = c.Lessons.Select(l => l.LessonID).ToList()
                })
                .ToListAsync();

            if (courses == null || !courses.Any())
                return NotFound();

            return Ok(courses);
        }

        // GET: api/Students/{userId}/EnrolledCourses - Get all courses that a student is enrolled in
        [HttpGet("{userId}/EnrolledCourses")]
        public async Task<ActionResult<IEnumerable<CourseDTO>>> GetEnrolledCourses(string userId)
        {
            // Check if the student exists
            var student = await _context.Students
                .Include(s => s.EnrolledCourses)
                .FirstOrDefaultAsync(s => s.UserID == userId);

            if (student == null)
            {
                return NotFound("Student not found");
            }

            if (student.EnrolledCourses.Count == 0)
                return NotFound("No enrolled courses");

            var courses = student.EnrolledCourses
                .Select(c => new CourseDTO
                {
                    CourseID = c.CourseID,
                    Title = c.Title,
                    Description = c.Description,
                    Tags = c.Tags,
                    UserID = c.UserID,
                    UserName = c.UserName,
                    CreatedDate = c.CreatedDate,
                    UpdateDate = c.UpdateDate,
                    LessonIds = c.Lessons.Select(l => l.LessonID).ToList()
                }).ToList();

            return Ok(courses);
        }

        // GET: api/Students/{userId}/Progress - Get all progress records for a student
        [HttpGet("{userId}/Progress")]
        public async Task<ActionResult<IEnumerable<CourseProgressDTO>>> GetStudentProgress(string userId)
        {
            // Check if the student exists
            var student = await _context.Students.FindAsync(userId);

            if (student == null)
            {
                return NotFound("Student not found");
            }

            // Load the progress records along with the associated Course and it's Lessons
            var progressRecords = await _context.CourseProgress
                .Include(p => p.Course)
                .ThenInclude(c => c.Lessons)
                .Where(p => p.UserID == userId)
                .ToListAsync();

            var progress = progressRecords.Select(p => new CourseProgressDTO
            {
                ProgressID = p.ProgressID,
                UserID = p.UserID,
                UserName = p.UserName,
                CourseID = p.CourseID,
                LessonsCompleted = p.LessonsCompleted,
                CompletedLessons = p.CompletedLessons,
                // Calculate completion percentage using Course.TotalLessons
                CompletionPercentage = p.Course == null || p.Course.Lessons.Count == 0 ? 0 : Math.Floor((double)p.LessonsCompleted / p.Course.Lessons.Count * 100),
                LastUpdated = p.UpdateDate
            }).ToList();
                

            if (progress == null || !progress.Any())
                return NotFound();

            return Ok(progress);
        }

        // POST: api/Students
        [HttpPost]
        public async Task<ActionResult<StudentDTO>> CreateStudent()
        {
            // Extract user information from the token
            var userIdClaim = User.FindFirst("sub")?.Value; // Cognito 'sub' (UUID)
            var usernameClaim = User.FindFirst("username")?.Value; // Cognito 'username'

            if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(usernameClaim))
            {
                return Unauthorized("Invalid token. User ID or username not found.");
            }

            // Check if the student already exsists
            var existingStudent = await _context.Students.FindAsync(userIdClaim);
            if (existingStudent != null)
                return Conflict("Student already exists");


            // Create new student object and save to the database
            var student = new Student(userIdClaim, usernameClaim);
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Prepare and return the DTO
            var studentDto = new StudentDTO
            {
                UserID = student.UserID,
                Username = student.UserName
            };            

            return CreatedAtAction(nameof(GetStudent), new { userid = student.UserID }, studentDto);
        }

        // PUT: api/Students/{userId} - Update student information
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateStudent(string userId, UpdateStudentDTO updateDto)
        {
            // Ensure the user can only update their profile
            if (!TryValidateUserId(userId))
            {
                return Forbid();
            }

            // Retrieve the student, including the profile
            var student = await _context.Students
                .Include(s => s.Profile)
                .FirstOrDefaultAsync(s => s.UserID == userId);

            if (student == null)
            {
                return NotFound("Student not found");
            }

            // Update basic student info
            student.UpdateName(updateDto.Username);

            // Update or create the profile
            if (updateDto.Profile != null)
            {
                var profileDto = updateDto.Profile;

                // Create a new StudentProfile instance based on the DTO
                var newProfile = new StudentProfile
                {
                    UserId = student.UserID,
                    EducationLevel = profileDto.EducationLevel,
                    PreferredContentTypes = profileDto.PreferredContentTypes,
                    SpecialConsiderations = profileDto.SpecialConsiderations,
                    AdditionalConsiderations = profileDto.AdditionalConsiderations
                };

                // Set the profile
                student.SetProfile(newProfile);
            }
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Students.Any(s => s.UserID == userId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // PUT: api/Students/{userId}/CompleteSetup - Mark the student as having completed setup
        [HttpPut("{userId}/CompleteSetup")]
        public async Task<IActionResult> CompleteSetup(string userId)
        {
            // Ensure the user can only update their profile
            if (!TryValidateUserId(userId))
            {
                return Forbid();
            }
            // Retrieve the student from the database
            var student = await _context.Students.FindAsync(userId);
            if (student == null)
            {
                return NotFound("Student not found");
            }
            // Mark the student as having completed setup
            student.MarkSetupComplete();
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Students.Any(s => s.UserID == userId))
                {
                    return NotFound("Student not found");
                }
                else
                {
                    throw;
                }
            }
            return NoContent();
        }

        // DELETE: api/Students/{userId}
        // This endpoint deletes the Student, progress records, and related CourseEnrollment entries
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteStudent(string userId)
        {
            // If the user is not an admin, ensure they can only delete their own profile
            var tokenUserId = User.FindFirst("sub")?.Value; // Cognito 'sub' (UUID)
            if (!IsAdmin() && tokenUserId != userId)
            {
                return Forbid(); // Prevent non-admin users from deleting other users
            }

            var student = await _context.Students
                .Include(s => s.CreatorCourses)
                    .ThenInclude(c => c.EnrolledStudents)
                .Include(s => s.ProgressRecords)
                .Include(s => s.EnrolledCourses)
                .FirstOrDefaultAsync(s => s.UserID == userId);

            if (student == null)
            {
                return NotFound();
            }
            // Dummy user ID for reassignment
            string dummyUserId = "00000000-0000-0000-0000-000000000000";
            // Retrieve the dummy user from the database
            var dummyUser = await _context.Students.FindAsync(dummyUserId);
            if (dummyUser == null)
            {
                // Create a dummy user if it doesn't exist
                dummyUser = new Student(dummyUserId, "Deleted User");
                _context.Students.Add(dummyUser);
                await _context.SaveChangesAsync();
            }

            // Process courses created by the student
            foreach (var course in student.CreatorCourses.ToList())
            {
                
                // Reassign the course to the dummy user
                course.ReassignCreator(dummyUserId, "Deleted User");
            }

            // Remove all progress records related to the student
            foreach (var progress in student.ProgressRecords.ToList())
            {
                _context.CourseProgress.Remove(progress);
            }

            // Remove the student's enrollment records
            // These should be automatically deleted with cascade delete
            // Clearing the collection is a defensive step to ensure in-memory data is consistent
            student.EnrolledCourses.Clear();

            // Finally, remove the student
            _context.Students.Remove(student);

            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}
