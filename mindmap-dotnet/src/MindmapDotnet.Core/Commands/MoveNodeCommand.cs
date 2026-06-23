using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Commands;

public class MoveNodeCommand : IUndoableCommand
{
    private readonly MindmapNode _node;
    private readonly MindmapNode _oldParent;
    private readonly int _oldIndex;
    private readonly MindmapNode _newParent;
    private readonly int _newIndex;

    public MoveNodeCommand(MindmapNode node, MindmapNode oldParent, int oldIndex, MindmapNode newParent, int newIndex)
    {
        _node = node;
        _oldParent = oldParent;
        _oldIndex = oldIndex;
        _newParent = newParent;
        _newIndex = newIndex;
    }

    public string Description => $"Move \"{_node.Text}\"";

    public void Do()
    {
        _oldParent.Children.Remove(_node);
        _node.ParentId = _newParent.Id;
        _newParent.Children.Insert(_newIndex, _node);
    }

    public void Undo()
    {
        _newParent.Children.Remove(_node);
        _node.ParentId = _oldParent.Id;
        _oldParent.Children.Insert(_oldIndex, _node);
    }
}
