using FindAFriend.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace FindAFriend.Core.Models;

public class AppUser : IdentityUser
{
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.JustSignedIn;
    public string? AvatarUrl { get; set; }
    public List<string> SocialMediaLinks { get; set; } = [];
    public bool IsBanned { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<VerificationPhoto> VerificationPhotos { get; set; } = [];
    public List<UserTag> UserTags { get; set; } = [];
    public List<Swipe> SwipesMade { get; set; } = [];
    public List<Swipe> SwipesReceived { get; set; } = [];
    public List<Report> ReportsMade { get; set; } = [];
    public List<Report> ReportsReceived { get; set; } = [];
}
