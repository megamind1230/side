using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;

namespace WeNeedA.Web.Services;

public class ListingService : IListingService
{
    private readonly AppDbContext _db;

    public ListingService(AppDbContext db) => _db = db;

    public Task<List<PersonListing>> GetListingsByVillageAsync(int villageId, VerificationStatus? status = null, int? tagId = null)
    {
        var query = _db.PersonListings
            .Include(p => p.PersonListingTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Village)
            .Where(p => p.VillageId == villageId);

        if (status.HasValue)
            query = query.Where(p => p.VerificationStatus == status.Value);

        if (tagId.HasValue)
            query = query.Where(p => p.PersonListingTags.Any(pt => pt.TagId == tagId.Value));

        return query.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public Task<PersonListing?> GetListingByIdAsync(int id) =>
        _db.PersonListings
            .Include(p => p.PersonListingTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Village).ThenInclude(v => v.City).ThenInclude(c => c.Governorate).ThenInclude(g => g.Country)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task CreateListingAsync(PersonListing listing, List<int> tagIds)
    {
        _db.PersonListings.Add(listing);
        await _db.SaveChangesAsync();

        foreach (var tagId in tagIds)
        {
            _db.PersonListingTags.Add(new PersonListingTag
            {
                PersonListingId = listing.Id,
                TagId = tagId
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task UpdateListingAsync(PersonListing listing, List<int> tagIds)
    {
        listing.UpdatedAt = DateTime.UtcNow;
        _db.PersonListings.Update(listing);

        var existingTags = _db.PersonListingTags.Where(pt => pt.PersonListingId == listing.Id);
        _db.PersonListingTags.RemoveRange(existingTags);

        foreach (var tagId in tagIds)
        {
            _db.PersonListingTags.Add(new PersonListingTag
            {
                PersonListingId = listing.Id,
                TagId = tagId
            });
        }
        await _db.SaveChangesAsync();
    }

    public Task<List<PersonListing>> GetListingsByUserAsync(string userId) =>
        _db.PersonListings
            .Include(p => p.PersonListingTags).ThenInclude(pt => pt.Tag)
            .Where(p => p.CreatedByUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<List<PersonListing>> GetListingsByStatusAsync(VerificationStatus status) =>
        _db.PersonListings
            .Include(p => p.PersonListingTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Village).ThenInclude(v => v.City).ThenInclude(c => c.Governorate).ThenInclude(g => g.Country)
            .Where(p => p.VerificationStatus == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task UpdateUserListingsVerificationStatusAsync(string userId, VerificationStatus status)
    {
        var listings = await _db.PersonListings
            .Where(p => p.CreatedByUserId == userId)
            .ToListAsync();

        foreach (var listing in listings)
        {
            listing.VerificationStatus = status;
        }

        await _db.SaveChangesAsync();
    }
}
