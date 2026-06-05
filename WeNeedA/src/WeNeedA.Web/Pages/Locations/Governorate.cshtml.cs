using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Locations;

public class GovernorateModel : PageModel
{
    private readonly ILocationService _locationService;

    public Governorate? Governorate { get; set; }
    public List<City> Cities { get; set; } = new();

    public GovernorateModel(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Governorate = await _locationService.GetGovernorateBySlugAsync(slug);
        if (Governorate == null) return NotFound();

        Cities = await _locationService.GetCitiesByGovernorateAsync(Governorate.Id);
        return Page();
    }
}
