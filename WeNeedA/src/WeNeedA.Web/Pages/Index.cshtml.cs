using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILocationService _locationService;

    public List<Country> Countries { get; set; } = new();

    public IndexModel(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task OnGetAsync()
    {
        Countries = await _locationService.GetAllCountriesAsync();
    }
}
