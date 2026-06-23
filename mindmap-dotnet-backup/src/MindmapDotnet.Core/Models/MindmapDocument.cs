namespace MindmapDotnet.Core.Models;

public class MindmapDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public MindmapNode RootNode { get; set; } = new();
    public string? FilePath { get; set; }
    public bool IsModified { get; set; }

    public int NodeCount => CountNodes(RootNode);

    private static int CountNodes(MindmapNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }
}
