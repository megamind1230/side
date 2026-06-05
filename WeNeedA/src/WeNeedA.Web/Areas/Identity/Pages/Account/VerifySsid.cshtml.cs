using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;

namespace WeNeedA.Web.Areas.Identity.Pages.Account;

[Authorize]
public class VerifySsidModel : PageModel
{
    private readonly UserManager<WeNeedAUser> _userManager;

    public VerifySsidModel(UserManager<WeNeedAUser> userManager)
    {
        _userManager = userManager;
    }

    public WeNeedAUser CurrentUser { get; set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string SsidNumber { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUser = (await _userManager.GetUserAsync(User))!;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        CurrentUser = (await _userManager.GetUserAsync(User))!;
        if (CurrentUser.IsSsidVerified)
        {
            return RedirectToPage();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        CurrentUser.SsidNumber = Input.SsidNumber;
        await _userManager.UpdateAsync(CurrentUser);

        TempData["Success"] = "SSID submitted for verification. An admin will review your request.";
        return RedirectToPage();
    }
}
