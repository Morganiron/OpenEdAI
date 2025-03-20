using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;

namespace OpenEdAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class LessonsController : BaseController
    {
        private ApplicationDbContext _context;

        public LessonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Lessons
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LessonDTO>>> GetLessons()
        {
            var lessons = await _context.Lessons
                .Select(l => new LessonDTO
                {
                    LessonID = l.LessonID,
                    Title = l.Title,
                    Description = l.Description,
                    ContentLink = l.ContentLink,
                    Tags = l.Tags,
                    CourseID = l.CourseID,
                    CreatedDate = l.CreatedDate,
                    UpdateDate = l.UpdateDate
                })
                .ToListAsync();

            return Ok(lessons);
        }

        // GET: api/Lessons/{id}
        [HttpGet("{lessonId}")]
        public async Task<ActionResult<LessonDTO>> GetLesson(int lessonId)
        {
            var lessonDto = await _context.Lessons
                .Where(l => l.LessonID == lessonId)
                .Select(l => new LessonDTO
                {
                    LessonID = l.LessonID,
                    Title = l.Title,
                    Description = l.Description,
                    ContentLink = l.ContentLink,
                    Tags = l.Tags,
                    CourseID = l.CourseID,
                    CreatedDate = l.CreatedDate,
                    UpdateDate = l.UpdateDate
                })
                .FirstOrDefaultAsync();

            if (lessonDto == null)
            {
                return BadRequest("Invalid lesson ID");
            }

            return Ok(lessonDto);
        }

        // GET api/Lessons/Course/{id}
        [HttpGet("Course/{courseId}")]
        public async Task<ActionResult<IEnumerable<LessonDTO>>> GetLessonsByCourse(int courseId)
        {
            if (!await _context.Lessons.AnyAsync(l => l.CourseID == courseId))
                return NotFound("No lessons found for this course");

            var lessons = await _context.Lessons
                .Where(l => l.CourseID == courseId)
                .Select(l => new LessonDTO
                {
                    LessonID = l.LessonID,
                    Title = l.Title,
                    Description = l.Description,
                    ContentLink = l.ContentLink,
                    Tags = l.Tags,
                    CourseID = l.CourseID,
                    CreatedDate = l.CreatedDate,
                    UpdateDate = l.UpdateDate
                })
                .ToListAsync();

            return Ok(lessons);
        }

        // POST: api/Lessons
        [HttpPost]
        public async Task<ActionResult<Lesson>> CreateLesson(CreateLessonDTO createDto)
        {
            // Check that course exists to associate the lesson
            var course = await _context.Courses.FindAsync(createDto.CourseID);
            if (course == null)
                return BadRequest("Course not found");

            // Enforce the authenticated user is the owner/creator of the course
            if (!TryValidateUserId(course.UserID))
            {
                return Forbid();
            }

            // Create a new Lesson entity using the provided data
            var lesson = new Lesson(createDto.Title, createDto.Description, createDto.ContentLink, createDto.Tags, createDto.CourseID);
            
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();

            // Map to a LessonDTO for output
            var lessonDto = new LessonDTO
            {
                LessonID = lesson.LessonID,
                Title = lesson.Title,
                Description = lesson.Description,
                ContentLink = lesson.ContentLink,
                Tags = lesson.Tags,
                CourseID = lesson.CourseID,
                CreatedDate = lesson.CreatedDate,
                UpdateDate = lesson.UpdateDate
            };

            return CreatedAtAction(nameof(GetLesson), new { id = lesson.LessonID }, lessonDto);
        }

        // PUT: api/Lessons/{id}
        [HttpPut("{lessonId}")]
        public async Task<IActionResult> UpdateLesson(int lessonId, UpdateLessonDTO updateDto)
        {
            var lesson = await _context.Lessons.FindAsync(lessonId);
            if (lesson == null)
                return NotFound();

            // Retrieve the course to verify ownership
            var course = await _context.Courses.FindAsync(lesson.CourseID);
            if (course == null)
            {
                return BadRequest("Course not found for the lesson.");
            }

            // Validate the user is the course owner for updates
            if (!TryValidateUserId(course.UserID))
            {
                return Forbid();
            }

            // TODO: If a course is shared (other users are enrolled)
            // contentlink updates should create a new course instead
            // I need to add something for if a link is reported as inappropriate or invalid

            // Update only allowed properties
            lesson.UpdateLesson(updateDto.Title, updateDto.Description, updateDto.Tags, updateDto.ContentLink);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Lessons.Any(e => e.LessonID == lessonId))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/Lessons/{id}
        [HttpDelete("{lessonId}")]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            var lesson = await _context.Lessons.FindAsync(lessonId);
            if (lesson == null)
                return NotFound();

            // Retrieve the course to verify ownership
            var course = await _context.Courses.FindAsync(lesson.CourseID);
            if (course == null)
            {
                return BadRequest("Course not found for the lesson.");
            }

            // Validate the user is the course owner
            if (!TryValidateUserId(course.UserID))
            {
                return Forbid();
            }

            // TODO: If a course is shared (other users are enrolled)
            // deleting a lesson should create a new course instead
            // I need to add something for if a lesson is reported as inappropriate or invalid

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
