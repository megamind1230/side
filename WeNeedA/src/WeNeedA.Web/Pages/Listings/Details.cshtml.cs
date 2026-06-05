using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Listings;

public class DetailsModel : PageModel
{
    private readonly IListingService _listingService;

    public PersonListing? Listing { get; set; }

    public DetailsModel(IListingService listingService)
    {
        _listingService = listingService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Listing = await _listingService.GetListingByIdAsync(id);
        if (Listing == null) return NotFound();
        return Page();
    }
}
