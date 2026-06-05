using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Locations;

public class CountryModel : PageModel
{
    private readonly ILocationService _locationService;

    public Country? Country { get; set; }
    public List<Governorate> Governorates { get; set; } = new();

    public CountryModel(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Country = await _locationService.GetCountryBySlugAsync(slug);
        if (Country == null) return NotFound();

        Governorates = await _locationService.GetGovernoratesByCountryAsync(Country.Id);
        return Page();
    }
}
