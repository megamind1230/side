using FindAFriend.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FindAFriend.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<VerificationPhoto> VerificationPhotos => Set<VerificationPhoto>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<UserTag> UserTags => Set<UserTag>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.VerificationStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            e.Property(u => u.SocialMediaLinks)
                .HasColumnType("jsonb");

            e.HasMany(u => u.VerificationPhotos)
                .WithOne(vp => vp.User)
                .HasForeignKey(vp => vp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(u => u.UserTags)
                .WithOne(ut => ut.User)
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(u => u.SwipesMade)
                .WithOne(s => s.SwiperUser)
                .HasForeignKey(s => s.SwiperUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(u => u.SwipesReceived)
                .WithOne(s => s.SwipedUser)
                .HasForeignKey(s => s.SwipedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(u => u.ReportsMade)
                .WithOne(r => r.ReporterUser)
                .HasForeignKey(r => r.ReporterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(u => u.ReportsReceived)
                .WithOne(r => r.ReportedUser)
                .HasForeignKey(r => r.ReportedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VerificationPhoto>(e =>
        {
            e.HasKey(vp => vp.Id);
            e.Property(vp => vp.ImageUrl).IsRequired().HasMaxLength(2048);
        });

        builder.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Name).IsUnique();
        });

        builder.Entity<UserTag>(e =>
        {
            e.HasKey(ut => new { ut.UserId, ut.TagId });

            e.HasOne(ut => ut.Tag)
                .WithMany(t => t.UserTags)
                .HasForeignKey(ut => ut.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Swipe>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Action).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(s => new { s.SwiperUserId, s.SwipedUserId }).IsUnique();
        });

        builder.Entity<Report>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Reason).HasConversion<string>().HasMaxLength(50);
        });
    }
}
