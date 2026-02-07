using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tria.Models;

namespace Tria.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<LearningBlock> LearningBlocks { get; set; } = null!;
        public DbSet<Module> Modules { get; set; } = null!;
        public DbSet<UserModuleProgress> UserModuleProgress { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<LearningBlock>()
                .HasMany(b => b.Modules)
                .WithOne(m => m.Block)
                .HasForeignKey(m => m.BlockId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Module>()
                .HasMany(m => m.UserProgress)
                .WithOne(u => u.Module)
                .HasForeignKey(u => u.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserModuleProgress>()
                .HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserModuleProgress>()
                .HasIndex(u => new { u.UserId, u.ModuleId })
                .IsUnique();
        }
    }
}
