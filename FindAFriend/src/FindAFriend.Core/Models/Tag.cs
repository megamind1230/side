namespace FindAFriend.Core.Models;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int PopularityCount { get; set; }

    public List<UserTag> UserTags { get; set; } = [];
}
