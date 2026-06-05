using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;

namespace WeNeedA.Web.Areas.Admin.Pages;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<WeNeedAUser> _userManager;

    public int TotalUsers { get; set; }
    public int VerifiedListings { get; set; }
    public int UnverifiedListings { get; set; }
    public int PendingSsidUsers { get; set; }

    public IndexModel(AppDbContext db, UserManager<WeNeedAUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task OnGetAsync()
    {
        TotalUsers = await _userManager.Users.CountAsync();
        VerifiedListings = await _db.PersonListings.CountAsync(p => p.VerificationStatus == VerificationStatus.Verified);
        UnverifiedListings = await _db.PersonListings.CountAsync(p => p.VerificationStatus == VerificationStatus.Unverified);
        PendingSsidUsers = await _userManager.Users.CountAsync(u => !string.IsNullOrEmpty(u.SsidNumber) && !u.IsSsidVerified);
    }
}
