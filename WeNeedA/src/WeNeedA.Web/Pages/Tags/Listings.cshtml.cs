using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;

namespace WeNeedA.Web.Pages.Tags;

public class ListingsModel : PageModel
{
    private readonly AppDbContext _db;

    public Tag? Tag { get; set; }
    public List<PersonListing> Listings { get; set; } = new();

    public ListingsModel(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        Tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
        if (Tag == null) return NotFound();

        Listings = await _db.PersonListingTags
            .Where(pt => pt.TagId == Tag.Id)
            .Include(pt => pt.PersonListing)
                .ThenInclude(pl => pl.PersonListingTags)
                .ThenInclude(pt => pt.Tag)
            .Include(pt => pt.PersonListing.Village)
                .ThenInclude(v => v.City)
                .ThenInclude(c => c.Governorate)
                .ThenInclude(g => g.Country)
            .Select(pt => pt.PersonListing)
            .OrderByDescending(pl => pl.CreatedAt)
            .ToListAsync();

        return Page();
    }
}
