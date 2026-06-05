using System;
using System.ComponentModel.DataAnnotations;

namespace NextLearn.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string DisplayName { get; set; } = "Guest";
    
    public string? Email { get; set; }
    
    public string? PasswordHash { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int TotalDecksCompleted { get; set; }
    
    public int TotalDecksShared { get; set; }
    
    public int CurrentStreak { get; set; }
    
    public DateTime LastActiveDate { get; set; } = DateTime.UtcNow.Date;
    
    public bool IsGuest { get; set; } = true;
}
