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
    public class StudentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Students
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudents()
        {
            return await _context.Students.ToListAsync();
        }

        // GET: api/Students/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<Student>> GetStudent(string userId)
        {
            var student = await _context.Students
                .Include(s => s.Courses)
                .Include(s => s.ProgressRecords)
                .FirstOrDefaultAsync(s => s.UserID == userId);

            if (student == null)
                return NotFound();

            return student;
        }

        // GET: api/Students/{userId}/Courses - Get all courses for a student
        [HttpGet("{userId}/Courses")]
        public async Task<ActionResult<IEnumerable<Course>>> GetStudentCourses(string userId)
        {
            var student = await _context.Students
                .Include(s => s.Courses)
                .FirstOrDefaultAsync(s => s.UserID == userId);
            
            if (student == null)
                return NotFound();

            return Ok(student.Courses);
        }

        // GET: api/Students/{userId}/Progress - Get all progress records for a student
        [HttpGet("{userId}/Progress")]
        public async Task<ActionResult<IEnumerable<CourseProgress>>> GetStudentProgress(string userId)
        {
            var student = await _context.Students
                .Include(s => s.ProgressRecords)
                .FirstOrDefaultAsync(s => s.UserID == userId);
            
            if (student == null)
                return NotFound();

            return Ok(student.ProgressRecords);
        }

        // PUT: api/Students/{userId} - Update student information
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateStudent(string userId, Student student)
        {
            if (userId != student.UserID)
                return BadRequest();

            _context.Entry(student).State = EntityState.Modified;
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Students.Any(s => s.UserID == userId))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }
    }
}
