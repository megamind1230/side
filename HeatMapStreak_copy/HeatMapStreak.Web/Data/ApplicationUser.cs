using HeatMapStreak.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace HeatMapStreak.Web.Data;

public class ApplicationUser : IdentityUser
{
    public ICollection<Habit> Habits { get; set; } = new List<Habit>();
}
