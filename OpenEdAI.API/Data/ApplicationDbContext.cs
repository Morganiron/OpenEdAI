using Microsoft.EntityFrameworkCore;
using OpenEdAI.API.Models;

namespace OpenEdAI.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructor that accepts DbContextOptions
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Database tables (DbSets)
        public DbSet<Student> Students { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<CourseProgress> CourseProgress { get; set; }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdateDate = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedDate = DateTime.UtcNow;
                    entry.Entity.UpdateDate = DateTime.UtcNow;
                }
            }
        }

        // Override OnModelCreating to configure relationships
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // One-to-Many: A Student creates many courses
            modelBuilder.Entity<Course>()
                .HasOne(c => c.Creator) // A Course has one creator
                .WithMany(s => s.CreatorCourses) // The Creator can have many courses
                .HasForeignKey(c => c.UserID)
                .OnDelete(DeleteBehavior.Restrict); // Prevents cascade delete on courses created by the user

            // Many-to-Many: A Course can be shared with many students
            modelBuilder.Entity<Course>()
                .HasMany(c => c.EnrolledStudents) // A Course can have many students
                .WithMany(s => s.EnrolledCourses) // A student can be enrolled in many courses
                .UsingEntity<Dictionary<string, object>>(
                    "CourseEnrollments",
                    r => r.HasOne<Student>()
                    .WithMany()
                    .HasForeignKey("StudentID")
                    .HasConstraintName("FK_StudentCourses_Student")
                    .OnDelete(DeleteBehavior.Cascade),
                    l => l.HasOne<Course>()
                    .WithMany()
                    .HasForeignKey("CourseID")
                    .HasConstraintName("FK_StudentCourses_Course")
                    .OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("CourseID", "StudentID");
                        j.ToTable("CourseEnrollments");
                    });

            // One-to-Many: A Course has multiple lessons
            modelBuilder.Entity<Course>()
                .HasMany(c => c.Lessons)
                .WithOne(l => l.Course)
                .HasForeignKey(l => l.CourseID)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-Many: A Student can have many progress records
            modelBuilder.Entity<CourseProgress>()
                .HasOne(cp => cp.Student)
                .WithMany(s => s.ProgressRecords)
                .HasForeignKey(cp => cp.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // One-To-One: A CourseProgress record tracks progress for a single course
            modelBuilder.Entity<CourseProgress>()
                .HasOne(cp => cp.Course)
                .WithMany()
                .HasForeignKey(cp => cp.CourseID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
