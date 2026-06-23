using MindmapDotnet.Core.Commands;
using MindmapDotnet.Core.Logging;
using MindmapDotnet.Core.Models;
using MindmapDotnet.Core.Serialization;
using MindmapDotnet.Core.Services.Layout;

namespace MindmapDotnet.Core.Services;

public class MindmapService
{
    private MindmapDocument _document = new();
    private readonly UndoRedoStack _undoStack = new();
    private ITreeLayoutAlgorithm _layoutAlgorithm = new LeftToRightLayout();

    public MindmapDocument Document => _document;
    public UndoRedoStack UndoRedo => _undoStack;
    public string LayoutName => _layoutAlgorithm.Name;

    public event Action? DocumentChanged;
    public event Action? LayoutChanged;

    public void SetLayout(ITreeLayoutAlgorithm algorithm)
    {
        _layoutAlgorithm = algorithm;
        LayoutChanged?.Invoke();
        DocumentChanged?.Invoke();
        AppLogger.Info("Layout changed to: {Name}", algorithm.Name);
    }

    public LayoutResult CalculateLayout(double nodeWidth, double nodeHeight, double hSpacing, double vSpacing)
    {
        return _layoutAlgorithm.CalculateLayout(_document.RootNode, nodeWidth, nodeHeight, hSpacing, vSpacing);
    }

    public void InsertNode(MindmapNode parent, string text)
    {
        var node = new MindmapNode { Text = text };
        var cmd = new InsertNodeCommand(parent, node);
        _undoStack.Execute(cmd);
        _document.IsModified = true;
        DocumentChanged?.Invoke();
    }

    public void InsertSibling(MindmapNode sibling, string text)
    {
        var parent = FindNode(_document.RootNode, sibling.ParentId ?? Guid.Empty);
        if (parent == null) return;
        var index = parent.Children.IndexOf(sibling) + 1;
        var node = new MindmapNode { Text = text };
        var cmd = new InsertNodeCommand(parent, node, index);
        _undoStack.Execute(cmd);
        _document.IsModified = true;
        DocumentChanged?.Invoke();
    }

    public void DeleteNode(MindmapNode node)
    {
        if (node == _document.RootNode) return;
        var parent = FindNode(_document.RootNode, node.ParentId ?? Guid.Empty);
        if (parent == null) return;
        var index = parent.Children.IndexOf(node);
        if (index < 0) return;
        var cmd = new DeleteNodeCommand(parent, node, index);
        _undoStack.Execute(cmd);
        _document.IsModified = true;
        DocumentChanged?.Invoke();
    }

    public void EditText(MindmapNode node, string newText)
    {
        var original = node.Text;
        if (original == newText) return;
        var cmd = new EditTextCommand(node, original, newText);
        _undoStack.Execute(cmd);
        _document.IsModified = true;
        DocumentChanged?.Invoke();
    }

    public void MoveNode(MindmapNode node, MindmapNode newParent, int newIndex = -1)
    {
        if (node == _document.RootNode) return;
        var oldParent = FindNode(_document.RootNode, node.ParentId ?? Guid.Empty);
        if (oldParent == null) return;
        var oldIndex = oldParent.Children.IndexOf(node);
        if (oldIndex < 0) return;
        if (newIndex < 0) newIndex = newParent.Children.Count;
        var cmd = new MoveNodeCommand(node, oldParent, oldIndex, newParent, newIndex);
        _undoStack.Execute(cmd);
        _document.IsModified = true;
        DocumentChanged?.Invoke();
    }

    public void Undo() { _undoStack.Undo(); _document.IsModified = true; DocumentChanged?.Invoke(); }
    public void Redo() { _undoStack.Redo(); _document.IsModified = true; DocumentChanged?.Invoke(); }

    public void NewDocument()
    {
        _document = new MindmapDocument();
        _undoStack.Clear();
        _document.IsModified = false;
        DocumentChanged?.Invoke();
        AppLogger.Info("New document created");
    }

    public void LoadFile(string filePath)
    {
        var markdown = File.ReadAllText(filePath);
        _document = MarkdownSerializer.Deserialize(markdown);
        _document.FilePath = filePath;
        _undoStack.Clear();
        _document.IsModified = false;
        DocumentChanged?.Invoke();
        AppLogger.Info("Loaded document from: {Path}", filePath);
    }

    public void SaveFile(string? filePath = null)
    {
        filePath ??= _document.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;
        var markdown = MarkdownSerializer.Serialize(_document);
        File.WriteAllText(filePath, markdown);
        _document.FilePath = filePath;
        _document.IsModified = false;
        DocumentChanged?.Invoke();
        AppLogger.Info("Saved document to: {Path}", filePath);
    }

    public static MindmapNode? FindNode(MindmapNode root, Guid id)
    {
        if (root.Id == id) return root;
        foreach (var child in root.Children)
        {
            var found = FindNode(child, id);
            if (found != null) return found;
        }
        return null;
    }

    public List<MindmapNode> GetAllNodes(MindmapNode root)
    {
        var nodes = new List<MindmapNode> { root };
        foreach (var child in root.Children)
            nodes.AddRange(GetAllNodes(child));
        return nodes;
    }

    public MindmapNode? FindParentOf(MindmapNode root, MindmapNode target)
    {
        foreach (var child in root.Children)
        {
            if (child.Id == target.Id) return root;
            var found = FindParentOf(child, target);
            if (found != null) return found;
        }
        return null;
    }
}
