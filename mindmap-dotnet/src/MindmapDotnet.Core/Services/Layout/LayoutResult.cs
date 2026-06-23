namespace MindmapDotnet.Core.Services.Layout;

public class LayoutResult
{
    public Dictionary<Guid, NodeLayout> NodeLayouts { get; init; } = [];
}

public class NodeLayout
{
    public required Guid NodeId { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}
