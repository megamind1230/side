using Microsoft.AspNetCore.Mvc.RazorPages;
using WeNeedA.Web.Models;
using WeNeedA.Web.Services;

namespace WeNeedA.Web.Pages.Tags;

public class IndexModel : PageModel
{
    private readonly ITagService _tagService;

    public List<Tag> Tags { get; set; } = new();

    public IndexModel(ITagService tagService)
    {
        _tagService = tagService;
    }

    public async Task OnGetAsync()
    {
        Tags = await _tagService.GetApprovedTagsAsync();
    }
}
