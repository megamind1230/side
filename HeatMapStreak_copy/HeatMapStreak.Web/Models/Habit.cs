using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HeatMapStreak.Web.Data;

namespace HeatMapStreak.Web.Models;

public class Habit
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(7)]
    public string Color { get; set; } = "#4CAF50";

    public GoalType GoalType { get; set; } = GoalType.None;

    public int? GoalValue { get; set; }

    public GoalPeriod? GoalPeriod { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    public ICollection<DayEntry> DayEntries { get; set; } = new List<DayEntry>();
}
