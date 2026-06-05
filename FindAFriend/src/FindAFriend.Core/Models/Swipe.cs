using FindAFriend.Core.Enums;

namespace FindAFriend.Core.Models;

public class Swipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SwiperUserId { get; set; } = string.Empty;
    public string SwipedUserId { get; set; } = string.Empty;
    public SwipeAction Action { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser SwiperUser { get; set; } = null!;
    public AppUser SwipedUser { get; set; } = null!;
}
