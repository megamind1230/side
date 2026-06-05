using WeNeedA.Web.Models;

namespace WeNeedA.Web.Services;

public interface ITagService
{
    Task<List<Tag>> GetApprovedTagsAsync();
    Task<List<Tag>> GetPendingTagsAsync();
    Task<Tag?> GetTagBySlugAsync(string slug);
    Task CreateTagAsync(Tag tag);
    Task ApproveTagAsync(int tagId);
    Task<List<Tag>> GetAllTagsAsync();
}
