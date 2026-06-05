using Microsoft.AspNetCore.Mvc;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationApiController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationApiController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    [HttpGet("governorates")]
    public async Task<IActionResult> GetGovernorates([FromQuery] int countryId)
    {
        var governorates = await _locationService.GetGovernoratesByCountryAsync(countryId);
        return Ok(governorates.Select(g => new { g.Id, g.Name }));
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities([FromQuery] int governorateId)
    {
        var cities = await _locationService.GetCitiesByGovernorateAsync(governorateId);
        return Ok(cities.Select(c => new { c.Id, c.Name }));
    }

    [HttpGet("villages")]
    public async Task<IActionResult> GetVillages([FromQuery] int cityId)
    {
        var villages = await _locationService.GetVillagesByCityAsync(cityId);
        return Ok(villages.Select(v => new { v.Id, v.Name }));
    }
}
