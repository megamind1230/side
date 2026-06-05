using HeatMapStreak.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HeatMapStreak.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<DayEntry> DayEntries => Set<DayEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Habit>(entity =>
        {
            entity.HasIndex(h => h.UserId);
            entity.HasMany(h => h.DayEntries)
                  .WithOne(d => d.Habit)
                  .HasForeignKey(d => d.HabitId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DayEntry>(entity =>
        {
            entity.HasKey(d => new { d.HabitId, d.Date });
        });
    }
}
