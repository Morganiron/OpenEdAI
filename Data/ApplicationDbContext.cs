using Microsoft.EntityFrameworkCore;
using OpenEdAI.Models;

namespace OpenEdAI.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructor that accepts DbContextOptions
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Database tables (DbSets)
        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<CourseProgress> CourseProgress { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Student>().HasBaseType<User>();

            modelBuilder.Entity<Course>()
            .HasMany(c => c.Lessons)
            .WithOne(l => l.Course)
            .HasForeignKey(l => l.CourseID)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseProgress>()
                .Property(cp => cp.CompletedLessonsJson)
                .HasColumnType("json"); // Ensures it's stored as JSON in MySQL

            modelBuilder.Entity<CourseProgress>()
            .HasOne(cp => cp.Course)
            .WithMany()
            .HasForeignKey(cp => cp.CourseID);

            modelBuilder.Entity<CourseProgress>()
            .HasOne(cp => cp.User)
            .WithMany()
            .HasForeignKey(cp => cp.Owner);
        }
    }
}
