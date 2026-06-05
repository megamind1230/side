namespace FindAFriend.Core.Models;

public class VerificationPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DayNumber { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
}
