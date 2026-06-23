using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Commands;

public class DeleteNodeCommand(MindmapNode parent, MindmapNode node, int index) : IUndoableCommand
{
    private readonly MindmapNode _parent = parent;
    private readonly MindmapNode _node = node;
    private readonly int _index = index;

    public string Description => $"Delete \"{_node.Text}\"";

    public void Do()
    {
        _parent.Children.Remove(_node);
    }

    public void Undo()
    {
        _node.ParentId = _parent.Id;
        _parent.Children.Insert(_index, _node);
    }
}
