using Microsoft.EntityFrameworkCore;
using WeNeedA.Web.Data;
using WeNeedA.Web.Models;

namespace WeNeedA.Web.Services;

public class TagService : ITagService
{
    private readonly AppDbContext _db;

    public TagService(AppDbContext db) => _db = db;

    public Task<List<Tag>> GetApprovedTagsAsync() =>
        _db.Tags.Where(t => t.IsApproved).OrderBy(t => t.Name).ToListAsync();

    public Task<List<Tag>> GetPendingTagsAsync() =>
        _db.Tags.Where(t => !t.IsApproved).OrderBy(t => t.Name).ToListAsync();

    public Task<Tag?> GetTagBySlugAsync(string slug) =>
        _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);

    public async Task CreateTagAsync(Tag tag)
    {
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
    }

    public async Task ApproveTagAsync(int tagId)
    {
        var tag = await _db.Tags.FindAsync(tagId);
        if (tag != null)
        {
            tag.IsApproved = true;
            await _db.SaveChangesAsync();
        }
    }

    public Task<List<Tag>> GetAllTagsAsync() =>
        _db.Tags.OrderBy(t => t.Name).ToListAsync();
}
