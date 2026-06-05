using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Models;

namespace WeNeedA.Web.Data;

public class AppDbContext : IdentityDbContext<WeNeedAUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Village> Villages => Set<Village>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PersonListing> PersonListings => Set<PersonListing>();
    public DbSet<PersonListingTag> PersonListingTags => Set<PersonListingTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Country>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        });

        builder.Entity<Governorate>(e =>
        {
            e.HasIndex(g => g.Slug).IsUnique();
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.Property(g => g.Slug).HasMaxLength(200).IsRequired();
            e.HasOne(g => g.Country)
                .WithMany(c => c.Governorates)
                .HasForeignKey(g => g.CountryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<City>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(200).IsRequired();
            e.HasOne(c => c.Governorate)
                .WithMany(g => g.Cities)
                .HasForeignKey(c => c.GovernorateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Village>(e =>
        {
            e.HasIndex(v => v.Slug).IsUnique();
            e.Property(v => v.Name).HasMaxLength(200).IsRequired();
            e.Property(v => v.Slug).HasMaxLength(200).IsRequired();
            e.HasOne(v => v.City)
                .WithMany(c => c.Villages)
                .HasForeignKey(v => v.CityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Tag>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        });

        builder.Entity<PersonListing>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.PhoneNumber).HasMaxLength(50);
            e.Property(p => p.LocationDetail).HasMaxLength(500);
            e.HasOne(p => p.Village)
                .WithMany(v => v.PersonListings)
                .HasForeignKey(p => p.VillageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PersonListingTag>(e =>
        {
            e.HasKey(pt => new { pt.PersonListingId, pt.TagId });
            e.HasOne(pt => pt.PersonListing)
                .WithMany(p => p.PersonListingTags)
                .HasForeignKey(pt => pt.PersonListingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pt => pt.Tag)
                .WithMany(t => t.PersonListingTags)
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
