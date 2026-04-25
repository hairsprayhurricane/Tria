using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tria.Models;

namespace Tria.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserLessonProgress> UserLessonProgress { get; set; } = null!;
    public DbSet<UserTestAttempt> UserTestAttempts { get; set; } = null!;
    public DbSet<UserCourseAssignment> UserCourseAssignments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserLessonProgress>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserLessonProgress>()
            .HasIndex(u => new { u.UserId, u.LessonId })
            .IsUnique();

        builder.Entity<UserTestAttempt>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserCourseAssignment>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserCourseAssignment>()
            .HasIndex(u => new { u.UserId, u.CourseId })
            .IsUnique();

        // Seed roles: Admin, Teacher, Student, Expert
        // ConcurrencyStamp is pinned to static GUIDs to prevent EF non-deterministic model warnings.
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "role-admin",   Name = "Admin",   NormalizedName = "ADMIN",   ConcurrencyStamp = "a1b2c3d4-0001-0000-0000-000000000001" },
            new IdentityRole { Id = "role-teacher", Name = "Teacher", NormalizedName = "TEACHER", ConcurrencyStamp = "a1b2c3d4-0001-0000-0000-000000000002" },
            new IdentityRole { Id = "role-student", Name = "Student", NormalizedName = "STUDENT", ConcurrencyStamp = "a1b2c3d4-0001-0000-0000-000000000003" },
            new IdentityRole { Id = "role-expert",  Name = "Expert",  NormalizedName = "EXPERT",  ConcurrencyStamp = "a1b2c3d4-0001-0000-0000-000000000004" }
        );
    }
}
