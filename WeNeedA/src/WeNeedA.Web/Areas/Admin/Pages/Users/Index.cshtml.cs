using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Areas.Admin.Pages.Users;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly UserManager<WeNeedAUser> _userManager;
    private readonly IListingService _listingService;

    public List<WeNeedAUser> Users { get; set; } = new();

    public UsersModel(UserManager<WeNeedAUser> userManager, IListingService listingService)
    {
        _userManager = userManager;
        _listingService = listingService;
    }

    public async Task OnGetAsync()
    {
        Users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveSsidAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        user.IsSsidVerified = true;
        user.IsEmailVerified = true;
        await _userManager.UpdateAsync(user);

        await _listingService.UpdateUserListingsVerificationStatusAsync(user.Id, VerificationStatus.Verified);

        TempData["Success"] = $"SSID approved for {user.Email}. Their listings are now marked as Verified.";
        return RedirectToPage();
    }
}
