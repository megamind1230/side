using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextLearn.Desktop.Models;

/// <summary>Daily study activity record for a user.</summary>
public class DailyActivity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public int PagesViewed { get; set; }

    public int MinutesLearned { get; set; }
}
