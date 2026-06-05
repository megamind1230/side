using FindAFriend.Core.Enums;

namespace FindAFriend.Core.Models;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReporterUserId { get; set; } = string.Empty;
    public string ReportedUserId { get; set; } = string.Empty;
    public ReportReason Reason { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public AppUser ReporterUser { get; set; } = null!;
    public AppUser ReportedUser { get; set; } = null!;
}
