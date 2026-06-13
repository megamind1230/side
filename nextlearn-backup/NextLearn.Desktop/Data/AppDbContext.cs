using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<ActiveLearning> ActiveLearning => Set<ActiveLearning>();
    public DbSet<DailyActivity> DailyActivities => Set<DailyActivity>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "nextlearn");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "nextlearn.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Deck>()
            .HasIndex(d => d.Title);

        modelBuilder.Entity<Page>()
            .HasIndex(p => new { p.DeckId, p.PageNumber })
            .IsUnique();

        modelBuilder.Entity<UserProgress>()
            .HasIndex(up => new { up.UserId, up.DeckId })
            .IsUnique();

        modelBuilder.Entity<ActiveLearning>()
            .HasIndex(al => new { al.UserId, al.Slot })
            .IsUnique();

        modelBuilder.Entity<DailyActivity>()
            .HasIndex(da => new { da.UserId, da.Date })
            .IsUnique();

    }
}
