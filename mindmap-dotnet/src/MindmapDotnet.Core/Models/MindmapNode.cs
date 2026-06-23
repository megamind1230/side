namespace MindmapDotnet.Core.Models;

public class MindmapNode
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public List<MindmapNode> Children { get; init; } = [];
    public string Color { get; set; } = "#4488cc";
    public bool IsCollapsed { get; set; }

    public MindmapNode ShallowCopy()
    {
        return new MindmapNode
        {
            Id = Id,
            Text = Text,
            ParentId = ParentId,
            Color = Color,
            IsCollapsed = IsCollapsed,
            Children = [..Children]
        };
    }
}
