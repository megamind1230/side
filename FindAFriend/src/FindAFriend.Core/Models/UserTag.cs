namespace FindAFriend.Core.Models;

public class UserTag
{
    public string UserId { get; set; } = string.Empty;
    public Guid TagId { get; set; }

    public AppUser User { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
