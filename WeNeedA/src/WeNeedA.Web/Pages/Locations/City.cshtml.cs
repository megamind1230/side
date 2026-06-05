using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Locations;

public class CityModel : PageModel
{
    private readonly ILocationService _locationService;

    public City? City { get; set; }
    public List<Village> Villages { get; set; } = new();

    public CityModel(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        City = await _locationService.GetCityBySlugAsync(slug);
        if (City == null) return NotFound();

        Villages = await _locationService.GetVillagesByCityAsync(City.Id);
        return Page();
    }
}
