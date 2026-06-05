using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;
using WeNeedA.Web.Models.Enums;

namespace WeNeedA.Web.Areas.Admin.Pages.Listings;

[Authorize(Roles = "Admin")]
public class ListingsModel : PageModel
{
    private readonly AppDbContext _db;

    public List<PersonListing> Listings { get; set; } = new();
    public string CurrentStatus { get; set; } = "All";

    public ListingsModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnGetAsync(string status = "All")
    {
        CurrentStatus = status;
        var query = _db.PersonListings
            .Include(p => p.Village)
            .Include(p => p.PersonListingTags).ThenInclude(pt => pt.Tag)
            .AsQueryable();

        if (status != "All" && Enum.TryParse<VerificationStatus>(status, out var vs))
        {
            query = query.Where(p => p.VerificationStatus == vs);
        }

        Listings = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int listingId)
    {
        var listing = await _db.PersonListings.FindAsync(listingId);
        if (listing != null)
        {
            _db.PersonListings.Remove(listing);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Listing deleted.";
        }
        return RedirectToPage();
    }
}
