using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Listings;

[Authorize]
public class MyListingsModel : PageModel
{
    private readonly IListingService _listingService;
    private readonly UserManager<WeNeedAUser> _userManager;

    public List<PersonListing> Listings { get; set; } = new();

    public MyListingsModel(IListingService listingService, UserManager<WeNeedAUser> userManager)
    {
        _listingService = listingService;
        _userManager = userManager;
    }

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            Listings = await _listingService.GetListingsByUserAsync(user.Id);
        }
    }
}
