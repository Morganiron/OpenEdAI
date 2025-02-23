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
    public class CourseProgressController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CourseProgressController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/CourseProgress
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CourseProgress>>> GetProgress()
        {
            return await _context.CourseProgress.ToListAsync();
        }

        // GET: api/CourseProgress/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<CourseProgress>> GetProgress(int id)
        {
            var progress = await _context.CourseProgress.FindAsync(id);
            if (progress == null) return NotFound();

            return progress;
        }

        // POST: api/CourseProgress
        [HttpPost]
        public async Task<ActionResult<CourseProgress>> CreateProgress(CourseProgress progress)
        {
            _context.CourseProgress.Add(progress);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProgress), new { id = progress.ProgressID }, progress);
        }

        // PUT: api/CourseProgress
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProgress(int id, CourseProgress progress)
        {
            if (id != progress.ProgressID) return BadRequest();
            _context.Entry(progress).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.CourseProgress.Any(e => e.ProgressID == id))
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

        // DELETE: api/CourseProgress/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProgress(int id)
        {
            var progress = await _context.CourseProgress.FindAsync(id);
            if (progress == null) return NotFound();

            _context.CourseProgress.Remove(progress);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
