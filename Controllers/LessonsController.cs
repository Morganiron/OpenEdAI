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
    public class LessonsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LessonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Lessons
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Lesson>>> GetLessons()
        {
            return await _context.Lessons.ToListAsync();
        }

        // GET: api/Lessons/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetLesson(int id)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Course)
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.LessonID == id);

            if (lesson == null) 
                return NotFound();

            return Ok(new
            {
                lesson.Title,
                lesson.Description,
                lesson.ContentLink,
                lesson.CourseID,
                Course = new
                {
                    lesson.Course.Title,
                    lesson.Course.Description,
                    lesson.Course.Tags,
                    lesson.Course.Owner,
                    lesson.Course.CreatedBy,
                    Use = new
                    {
                        lesson.Course.User.Name,
                        lesson.Course.User.Email
                    }
                },
                lesson.Owner,
                lesson.UserName,
                User = new
                {
                    lesson.User.Name,
                    lesson.User.Email
                },
            });
        }

        // GET api/Lessons/Course/{id}
        [HttpGet("Course/{courseId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetLessonsByCourse(int courseId)
        {
            if (!await _context.Lessons.AnyAsync(l => l.CourseID == courseId))
                return NotFound("No lessons found for this course");

            var lessons = await _context.Lessons
                .Include(l => l.Course)
                .Include(l => l.User) 
                .Where(l => l.CourseID == courseId)
                .ToListAsync();

            return Ok(lessons.Select(lesson => new
            {
                lesson.Title,
                lesson.Description,
                lesson.ContentLink,
                lesson.CourseID,
                Course = new
                {
                    lesson.Course.Title,
                    lesson.Course.Description,
                    lesson.Course.Tags,
                    lesson.Course.Owner,
                    lesson.Course.CreatedBy,
                    Use = new
                    {
                        lesson.Course.User.Name,
                        lesson.Course.User.Email
                    }
                },
                lesson.Owner,
                lesson.UserName,
                User = new
                {
                    lesson.User.Name,
                    lesson.User.Email
                },
            }));
        }

        // POST: api/Lessons
        [HttpPost]
        public async Task<ActionResult<Lesson>> CreateLesson(Lesson lesson)
        {
            var course = await _context.Courses.FindAsync(lesson.CourseID);
            if (course == null)
                return BadRequest("Course not found");

            var user = await _context.Users.FindAsync(lesson.Owner);
            if (user == null)
                return BadRequest("User not found");

            // Associated the lesson with the course and user
            lesson.GetType().GetProperty("Course").SetValue(lesson, course);
            lesson.GetType().GetProperty("User").SetValue(lesson, user);

            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLesson), new { id = lesson.LessonID }, lesson);
        }

        // PUT: api/Lessons/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLesson(int id, Lesson lesson)
        {
            if (id != lesson.LessonID)
                return BadRequest();

            _context.Entry(lesson).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Lessons.Any(e => e.LessonID == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/Lessons/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLesson(int id)
        {
            var lesson = await _context.Lessons.FindAsync(id);
            if (lesson == null)
                return NotFound();

            _context.Lessons.Remove(lesson);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
