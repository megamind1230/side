using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Areas.Admin.Pages.Tags;

[Authorize(Roles = "Admin")]
public class TagsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITagService _tagService;

    public List<Tag> ApprovedTags { get; set; } = new();
    public List<Tag> PendingTags { get; set; } = new();

    public TagsModel(AppDbContext db, ITagService tagService)
    {
        _db = db;
        _tagService = tagService;
    }

    public async Task OnGetAsync()
    {
        ApprovedTags = await _tagService.GetApprovedTagsAsync();
        PendingTags = await _tagService.GetPendingTagsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Success"] = "Tag name is required.";
            return RedirectToPage();
        }

        var slug = name.ToLower().Replace(" ", "-").Replace("/", "-");
        if (await _db.Tags.AnyAsync(t => t.Slug == slug))
        {
            TempData["Success"] = "Tag already exists.";
            return RedirectToPage();
        }

        var tag = new Tag
        {
            Name = name.Trim(),
            Slug = slug,
            IsApproved = true,
        };
        await _tagService.CreateTagAsync(tag);

        TempData["Success"] = $"Tag '{tag.Name}' created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(int tagId)
    {
        await _tagService.ApproveTagAsync(tagId);
        TempData["Success"] = "Tag approved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int tagId)
    {
        var tag = await _db.Tags.FindAsync(tagId);
        if (tag != null)
        {
            _db.Tags.Remove(tag);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Tag deleted.";
        }
        return RedirectToPage();
    }
}
