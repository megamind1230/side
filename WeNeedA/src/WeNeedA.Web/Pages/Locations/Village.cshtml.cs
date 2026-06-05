using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Locations;

public class VillageModel : PageModel
{
    private readonly ILocationService _locationService;
    private readonly IListingService _listingService;
    private readonly ITagService _tagService;

    public Village? Village { get; set; }
    public List<PersonListing> Listings { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public int? SelectedTagId { get; set; }
    public string? CurrentStatus { get; set; }

    public VillageModel(ILocationService locationService, IListingService listingService, ITagService tagService)
    {
        _locationService = locationService;
        _listingService = listingService;
        _tagService = tagService;
    }

    public async Task<IActionResult> OnGetAsync(string slug, int? tagId, string? status)
    {
        Village = await _locationService.GetVillageBySlugAsync(slug);
        if (Village == null) return NotFound();

        SelectedTagId = tagId;
        CurrentStatus = status;

        VerificationStatus? vs = status switch
        {
            "Verified" => VerificationStatus.Verified,
            "Unverified" => VerificationStatus.Unverified,
            "Admin" => VerificationStatus.Admin,
            _ => null
        };

        Listings = await _listingService.GetListingsByVillageAsync(Village.Id, vs, tagId);
        Tags = await _tagService.GetApprovedTagsAsync();

        return Page();
    }
}
