using WeNeedA.Web.Models;

namespace WeNeedA.Web.Services;

public interface ILocationService
{
    Task<List<Country>> GetAllCountriesAsync();
    Task<List<Governorate>> GetGovernoratesByCountryAsync(int countryId);
    Task<List<City>> GetCitiesByGovernorateAsync(int governorateId);
    Task<List<Village>> GetVillagesByCityAsync(int cityId);
    Task<Country?> GetCountryBySlugAsync(string slug);
    Task<Governorate?> GetGovernorateBySlugAsync(string slug);
    Task<City?> GetCityBySlugAsync(string slug);
    Task<Village?> GetVillageBySlugAsync(string slug);
}
