using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;

namespace WeNeedA.Web.Services;

public interface IListingService
{
    Task<List<PersonListing>> GetListingsByVillageAsync(int villageId, VerificationStatus? status = null, int? tagId = null);
    Task<PersonListing?> GetListingByIdAsync(int id);
    Task CreateListingAsync(PersonListing listing, List<int> tagIds);
    Task UpdateListingAsync(PersonListing listing, List<int> tagIds);
    Task<List<PersonListing>> GetListingsByUserAsync(string userId);
    Task<List<PersonListing>> GetListingsByStatusAsync(VerificationStatus status);
    Task UpdateUserListingsVerificationStatusAsync(string userId, VerificationStatus status);
}
