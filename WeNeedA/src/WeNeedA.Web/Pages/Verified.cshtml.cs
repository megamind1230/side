using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages;

public class VerifiedModel : PageModel
{
    private readonly IListingService _listingService;

    public List<PersonListing> Listings { get; set; } = new();

    public VerifiedModel(IListingService listingService)
    {
        _listingService = listingService;
    }

    public async Task OnGetAsync()
    {
        Listings = await _listingService.GetListingsByStatusAsync(VerificationStatus.Verified);
    }
}
