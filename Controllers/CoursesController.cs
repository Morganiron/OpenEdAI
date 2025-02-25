using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.Data;
using OpenEdAI.Models;

namespace OpenEdAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Courses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Course>>> GetCourses()
        {
            var courses = await _context.Courses
                .Select(c => new
                {
                    c.CourseID,
                    c.Title,
                    c.Description,
                    c.Tags,
                    c.Owner,
                    c.CreatedBy,
                    c.CreatedDate,
                    c.UpdateDate,
                    TotalLessons = c.Lessons.Count

                })
                .ToListAsync();

            return Ok(courses);
        }

        // GET: api/Courses/{id} - Get a course and the student's progress
        [HttpGet("{id}")]
        public async Task<ActionResult<Course>> GetCourse(int id)
        {
            // Get userId from Cognito Token
            var studentId = User.FindFirst("sub")?.Value; // Get 'sub' from JWT token
            if (string.IsNullOrEmpty(studentId)) return Unauthorized("User ID not found in token");

            var course = await _context.Courses
                .Include(c => c.Lessons)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CourseID == id);

            if (course == null) return NotFound("Course not found");

            // Retrieve student's progress
            var progress = await _context.CourseProgress
                .Where(p => p.Owner == studentId && p.CourseID == id)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                course.CourseID,
                course.Title,
                course.Description,
                course.Tags,
                course.Owner,
                OwnerInfo = course.User == null ? null : new
                {
                    course.User.Name,
                    course.User.Email
                },
                course.CreatedBy,
                course.CreatedDate,
                course.UpdateDate,

                // Lessons for this course
                Lessons = course.Lessons.Select(lesson => new
                {
                    lesson.LessonID,
                    lesson.CourseID,
                    lesson.Title,
                    lesson.Description,
                    lesson.ContentLink,
                    lesson.Tags,
                    lesson.CreatedDate,
                }),

                // Student's progress in this course (if it exists)
                Progress = progress == null ? null : new
                {
                    progress.ProgressID,
                    progress.Owner,
                    progress.CourseID,
                    progress.LessonsCompleted,
                    progress.CompletedLessons,
                    progress.CompletionPercentage,
                    progress.LastUpdated

                }
            });
        }

        // POST: api/Courses
        [HttpPost]
        public async Task<ActionResult<Course>> CreateCourse(Course course)
        {
            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCourse), new { id = course.CourseID }, course);
        }

        // PUT: api/Courses
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, Course course)
        {
            if (id != course.CourseID) return BadRequest();
            _context.Entry(course).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Courses.Any(e => e.CourseID == id))
                    return NotFound();
                else 
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/Courses
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
