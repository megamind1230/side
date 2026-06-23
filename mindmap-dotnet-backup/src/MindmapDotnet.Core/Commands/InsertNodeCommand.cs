using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Commands;

public class InsertNodeCommand(MindmapNode parent, MindmapNode newNode, int index = -1) : IUndoableCommand
{
    private readonly MindmapNode _parent = parent;
    private readonly MindmapNode _newNode = newNode;
    private readonly int _index = index >= 0 ? index : parent.Children.Count;

    public string Description => $"Insert \"{_newNode.Text}\"";
    public string OriginalText => string.Empty;
    public string NewText => string.Empty;

    public void Do()
    {
        _newNode.ParentId = _parent.Id;
        _parent.Children.Insert(_index, _newNode);
    }

    public void Undo()
    {
        _parent.Children.Remove(_newNode);
        _newNode.ParentId = null;
    }
}
