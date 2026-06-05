using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Listings;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IListingService _listingService;
    private readonly ILocationService _locationService;
    private readonly ITagService _tagService;
    private readonly UserManager<WeNeedAUser> _userManager;

    public CreateModel(
        IListingService listingService,
        ILocationService locationService,
        ITagService tagService,
        UserManager<WeNeedAUser> userManager)
    {
        _listingService = listingService;
        _locationService = locationService;
        _tagService = tagService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> CountriesSelectList { get; set; } = new();
    public List<SelectListItem> GovernoratesSelectList { get; set; } = new();
    public List<SelectListItem> CitiesSelectList { get; set; } = new();
    public List<SelectListItem> VillagesSelectList { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();

    public class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? LocationDetail { get; set; }
        public int VillageId { get; set; }
    }

    [BindProperty]
    public List<int> SelectedTagIds { get; set; } = new();

    public async Task OnGetAsync()
    {
        var countries = await _locationService.GetAllCountriesAsync();
        CountriesSelectList = countries.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
        Tags = await _tagService.GetApprovedTagsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            var countries = await _locationService.GetAllCountriesAsync();
            CountriesSelectList = countries.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
            Tags = await _tagService.GetApprovedTagsAsync();
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var status = user.IsSsidVerified ? VerificationStatus.Verified : VerificationStatus.Unverified;

        var listing = new PersonListing
        {
            Name = Input.Name,
            PhoneNumber = Input.PhoneNumber,
            LocationDetail = Input.LocationDetail,
            VillageId = Input.VillageId,
            VerificationStatus = status,
            CreatedByUserId = user.Id,
        };

        await _listingService.CreateListingAsync(listing, SelectedTagIds);

        TempData["Success"] = "Listing created successfully!";
        return RedirectToPage("/Listings/Details", new { id = listing.Id });
    }
}
