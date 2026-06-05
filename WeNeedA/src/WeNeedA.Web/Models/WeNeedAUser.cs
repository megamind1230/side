using Microsoft.AspNetCore.Identity;

namespace WeNeedA.Web.Models;

public class WeNeedAUser : IdentityUser
{
    public bool IsEmailVerified { get; set; }
    public bool IsSsidVerified { get; set; }
    public string? SsidNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
