using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Data;
using OpenEdAI.API.DTOs;
using OpenEdAI.API.Models;

namespace OpenEdAI.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class CourseProgressController : BaseController
    {
        private ApplicationDbContext _context;

        public CourseProgressController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/CourseProgress
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CourseProgressDTO>>> GetProgress()
        {
            var progressList = await _context.CourseProgress
                .Include(cp => cp.Course)
                .Select(cp => new CourseProgressDTO
                {
                    ProgressID = cp.ProgressID,
                    UserID = cp.UserID,
                    UserName = cp.UserName,
                    CourseID = cp.CourseID,
                    LessonsCompleted = cp.LessonsCompleted,
                    CompletedLessons = cp.CompletedLessons,
                    // Calculate CompletionPercentage and drop the decimal remainder without rounding
                    CompletionPercentage = cp.Course == null || cp.Course.Lessons.Count == 0 ? 0 : Math.Floor((double)cp.LessonsCompleted / cp.Course.Lessons.Count * 100),
                    LastUpdated = cp.UpdateDate
                })
                .ToListAsync();

            return Ok(progressList);
        }

        // GET: api/CourseProgress/user
        [HttpGet("user")]
        public async Task<ActionResult<IEnumerable<CourseProgressDTO>>> GetUserProgress()
        {
            // Get the user ID from the token
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Get the progress for the user
            var progressList = await _context.CourseProgress
                .Include(cp => cp.Course)
                .Where(cp => cp.UserID == userId)
                .Select(cp => new CourseProgressDTO
                {
                    ProgressID = cp.ProgressID,
                    UserID = cp.UserID,
                    UserName = cp.UserName,
                    CourseID = cp.CourseID,
                    LessonsCompleted = cp.LessonsCompleted,
                    CompletedLessons = cp.CompletedLessons,
                    CompletionPercentage = cp.Course == null || cp.Course.Lessons.Count == 0 ? 0 : Math.Floor((double)cp.LessonsCompleted / cp.Course.Lessons.Count * 100),
                    LastUpdated = cp.UpdateDate
                })
                .ToListAsync();

            return Ok(progressList);
        }


        // GET: api/CourseProgress/{id}
        [HttpGet("{progressId}")]
        public async Task<ActionResult<CourseProgressDTO>> GetProgress(int progressId)
        {
            var progress = await _context.CourseProgress
                .Include(cp => cp.Course)
                .Where(cp => cp.ProgressID == progressId)
                .Select(cp => new CourseProgressDTO
                {
                    ProgressID = cp.ProgressID,
                    UserID = cp.UserID,
                    UserName = cp.UserName,
                    CourseID = cp.CourseID,
                    LessonsCompleted = cp.LessonsCompleted,
                    CompletedLessons = cp.CompletedLessons,
                    CompletionPercentage = cp.Course == null || cp.Course.Lessons.Count == 0 ? 0 : Math.Floor((double)cp.LessonsCompleted / cp.Course.Lessons.Count * 100),
                    LastUpdated = cp.UpdateDate
                })
                .FirstOrDefaultAsync();

            if (progress == null) return NotFound();

            return Ok(progress);
        }

        // POST: api/CourseProgress
        [HttpPost]
        public async Task<ActionResult<CourseProgressDTO>> CreateProgress(CreateCourseProgressDTO createDto)
        {
            // Validate only the owner can create progress
            if (!TryValidateUserId(createDto.UserID))
            {
                return Forbid();
            }

            // Validate the referenced course exists
            var course = await _context.Courses.FindAsync(createDto.CourseID);
            if (course == null)
                return BadRequest("Course not found");

            var progress = new CourseProgress(createDto.UserID, createDto.UserName, createDto.CourseID);

            _context.CourseProgress.Add(progress);
            await _context.SaveChangesAsync();

            var progressDto = new CourseProgressDTO
            {
                ProgressID = progress.ProgressID,
                UserID = progress.UserID,
                UserName = progress.UserName,
                CourseID = progress.CourseID,
                LessonsCompleted = progress.LessonsCompleted,
                CompletedLessons = progress.CompletedLessons,
                CompletionPercentage = progress.Course == null || progress.Course.Lessons.Count == 0 ? 0 : Math.Floor((double)progress.LessonsCompleted / progress.Course.Lessons.Count * 100),
                LastUpdated = progress.UpdateDate
            };

            return CreatedAtAction(nameof(GetProgress), new { id = progress.ProgressID }, progressDto);
        }

        // PATCH: api/CourseProgress/{progressId}
        [HttpPatch("{progressId}")]
        public async Task<IActionResult> PatchProgress(int progressId, [FromBody] MarkLessonCompleteDTO patchDto)
        {

            // Get the progress with course and lessons
            var progress = await _context.CourseProgress
                .Include(cp => cp.Course)
                .ThenInclude(c => c.Lessons)
                .FirstOrDefaultAsync(cp => cp.ProgressID == progressId);

            if (progress == null)
                return NotFound();

            // Validate only the owner can update progress
            if (!TryValidateUserId(progress.UserID))
            {
                return Forbid();
            }

            // Ensure the lesson is part of the course
            if (!progress.Course.Lessons.Any(l => l.LessonID == patchDto.LessonID))
                return BadRequest("Lesson not found in course");

            // Mark the lesson as complete
            progress.MarkLessonCompleted(patchDto.LessonID);

            // Force EF core to detect the change in the CompletedLessonsJson property
            _context.Entry(progress).Property(p => p.CompletedLessonsJson).IsModified = true;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/CourseProgress/{id}
        [HttpDelete("{progressId}")]
        public async Task<IActionResult> DeleteProgress(int progressId)
        {
            var progress = await _context.CourseProgress.FindAsync(progressId);
            if (progress == null) return NotFound();

            // Validate only the owner can delete progress
            if (!TryValidateUserId(progress.UserID))
            {
                return Forbid();
            }

            _context.CourseProgress.Remove(progress);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
