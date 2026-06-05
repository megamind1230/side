using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;

namespace WeNeedA.Web.Services;

public class LocationService : ILocationService
{
    private readonly AppDbContext _db;

    public LocationService(AppDbContext db) => _db = db;

    public Task<List<Country>> GetAllCountriesAsync() =>
        _db.Countries.OrderBy(c => c.Name).ToListAsync();

    public Task<List<Governorate>> GetGovernoratesByCountryAsync(int countryId) =>
        _db.Governorates.Where(g => g.CountryId == countryId).OrderBy(g => g.Name).ToListAsync();

    public Task<List<City>> GetCitiesByGovernorateAsync(int governorateId) =>
        _db.Cities.Where(c => c.GovernorateId == governorateId).OrderBy(c => c.Name).ToListAsync();

    public Task<List<Village>> GetVillagesByCityAsync(int cityId) =>
        _db.Villages.Where(v => v.CityId == cityId).OrderBy(v => v.Name).ToListAsync();

    public Task<Country?> GetCountryBySlugAsync(string slug) =>
        _db.Countries.FirstOrDefaultAsync(c => c.Slug == slug);

    public Task<Governorate?> GetGovernorateBySlugAsync(string slug) =>
        _db.Governorates.Include(g => g.Country).FirstOrDefaultAsync(g => g.Slug == slug);

    public Task<City?> GetCityBySlugAsync(string slug) =>
        _db.Cities.Include(c => c.Governorate).ThenInclude(g => g.Country)
            .FirstOrDefaultAsync(c => c.Slug == slug);

    public Task<Village?> GetVillageBySlugAsync(string slug) =>
        _db.Villages.Include(v => v.City).ThenInclude(c => c.Governorate).ThenInclude(g => g.Country)
            .FirstOrDefaultAsync(v => v.Slug == slug);
}
