using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenEdAI.Data;
using OpenEdAI.Models;
using OpenEdAI.DTOs;

namespace OpenEdAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class CoursesController : BaseController
    {
        private ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        

        // GET: api/Courses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CourseDTO>>> GetCourses()
        {
            var courses = await _context.Courses
                .Include(c => c.Lessons)
                .Select(c => new CourseDTO
                {
                    CourseID = c.CourseID,
                    Title = c.Title,
                    Description = c.Description,
                    Tags = c.Tags,
                    UserID = c.UserID,
                    // Get current userName if available to reflect UserName updates
                    UserName = c.Creator != null ? c.Creator.UserName : c.UserName,
                    CreatedDate = c.CreatedDate,
                    UpdateDate = c.UpdateDate,
                    LessonIds = c.Lessons.Select(l => l.LessonID).ToList(),
                    EnrolledStudents = c.EnrolledStudents.Select(s => new EnrolledStudentDTO
                    {
                        UserID = s.UserID,
                        UserName = s.UserName
                    }).ToList()
                })
                .ToListAsync();

            return Ok(courses);
        }

        // GET: api/Courses/{id} - Get a course by ID with lessons and enrolled students
        [HttpGet("{courseId}")]
        public async Task<ActionResult<CourseDTO>> GetCourse(int courseId)
        {
           var course = await _context.Courses
                .Include(c => c.Lessons)
                .Include(c => c.EnrolledStudents)
                .FirstOrDefaultAsync(c => c.CourseID == courseId);

            if (course == null)
            {
                return NotFound("Course not found");
            }

            var courseDetail = new CourseDTO
            {
                CourseID = course.CourseID,
                Title = course.Title,
                Description = course.Description,
                Tags = course.Tags,
                UserID = course.UserID,
                UserName = course.Creator != null ? course.Creator.UserName : course.UserName,
                CreatedDate = course.CreatedDate,
                UpdateDate = course.UpdateDate,
                LessonIds = course.Lessons.Select(l => l.LessonID).ToList(),
                EnrolledStudents = course.EnrolledStudents.Select(s => new EnrolledStudentDTO
                {
                    UserID = s.UserID,
                    UserName = s.UserName
                }).ToList()
            };

            return Ok(courseDetail);
        }

        // POST: api/Courses
        [HttpPost]
        public async Task<ActionResult<CourseDTO>> CreateCourse(CreateCourseDTO createDto)
        {
            // Create a new Course instance using the provided data
            var course = new Course(createDto.Title, createDto.Description, createDto.Tags, createDto.UserID, createDto.UserName);

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            // Add student to the course as an enrolled student
            var creator = await _context.Students.FindAsync(course.UserID);
            if (creator != null)
            {
                course.EnrolledStudents.Add(creator);
                await _context.SaveChangesAsync();
            }

            // Reload the course to ensure the enrolled students collection is loaded
            await _context.Entry(course).Collection(c => c.EnrolledStudents).LoadAsync();

            var courseDto = new CourseDTO
            {
                CourseID = course.CourseID,
                Title = course.Title,
                Description = course.Description,
                Tags = course.Tags,
                UserID = course.UserID,
                UserName = course.Creator != null ? course.Creator.UserName : course.UserName,
                CreatedDate = course.CreatedDate,
                UpdateDate = course.UpdateDate,
                LessonIds = course.Lessons.Select(l => l.LessonID).ToList(),
                EnrolledStudents = course.EnrolledStudents.Select(s => new EnrolledStudentDTO
                {
                    UserID = s.UserID,
                    UserName = s.UserName
                }).ToList()
            };

            return CreatedAtAction(nameof(GetCourse), new { courseId = course.CourseID }, courseDto);
        }

        // POST: api/Courses/{courseId}/EnrollStudent/{studentId}
        [HttpPost("{courseId}/EnrollStudent/{studentId}")]
        public async Task<IActionResult> EnrollStudent(int courseId, string studentId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            var student = await _context.Students.FindAsync(studentId);

            if (course == null || student == null)
                return NotFound("Course or Student not found");

            // Add the student to the course's enrolled students collection
            course.EnrolledStudents.Add(student);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Courses
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, UpdateCourseDTO updateDto)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            course.UpdateCourse(updateDto.Title, updateDto.Description, updateDto.Tags);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Courses.Any(e => e.CourseID == id))
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

        // DELETE: api/Courses/{courseId}/UnenrollStudent/{studentId}
        [HttpDelete("{courseId}/UnenrollStudent/{studentId}")]
        public async Task<IActionResult> UnenrollStudent(int courseId, string studentId)
        {
            // Retrieve the course including it's EnrolledStudents collection
            var course = await _context.Courses
                .Include(c => c.EnrolledStudents)
                .FirstOrDefaultAsync(c => c.CourseID == courseId);

            if (course == null)
                return NotFound("Course not found");

            // Retrieve the student
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound("Stdudent not found");

            // Remove the student from the enrolled students collection
            if (!course.EnrolledStudents.Contains(student))
                return BadRequest("Student is not enrolled in this course");

            course.EnrolledStudents.Remove(student);

            // Save changes. Cascade delete should remove the corresponding join table entry
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
