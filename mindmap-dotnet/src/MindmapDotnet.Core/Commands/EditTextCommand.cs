using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Commands;

public class EditTextCommand(MindmapNode node, string originalText, string newText) : IUndoableCommand
{
    private readonly MindmapNode _node = node;
    private readonly string _originalText = originalText;
    private readonly string _newText = newText;

    public string Description => $"Edit \"{_originalText}\" -> \"{_newText}\"";

    public void Do() => _node.Text = _newText;
    public void Undo() => _node.Text = _originalText;
}
