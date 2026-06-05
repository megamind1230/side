using System.ComponentModel.DataAnnotations.Schema;

namespace HeatMapStreak.Web.Models;

public class DayEntry
{
    public int HabitId { get; set; }

    public DateOnly Date { get; set; }

    public bool IsCompleted { get; set; }

    [ForeignKey(nameof(HabitId))]
    public Habit Habit { get; set; } = null!;
}
