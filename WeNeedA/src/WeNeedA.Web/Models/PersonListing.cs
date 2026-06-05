using WeNeedA.Web.Models.Enums;

namespace WeNeedA.Web.Models;

public class PersonListing
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public string? PhoneNumber { get; set; }
    public string? SocialLinksJson { get; set; }
    public int VillageId { get; set; }
    public string? LocationDetail { get; set; }
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Village Village { get; set; } = null!;
    public ICollection<PersonListingTag> PersonListingTags { get; set; } = new List<PersonListingTag>();
}
