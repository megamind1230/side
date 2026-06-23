using MindmapDotnet.Core.Logging;

namespace MindmapDotnet.Core.Commands;

public class UndoRedoStack
{
    private readonly LinkedList<IUndoableCommand> _undoStack = [];
    private readonly LinkedList<IUndoableCommand> _redoStack = [];
    private const int MaxDepth = 50;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => _undoStack.Last?.Value?.Description;
    public string? RedoDescription => _redoStack.Last?.Value?.Description;

    public void Execute(IUndoableCommand command)
    {
        command.Do();
        _undoStack.AddLast(command);
        _redoStack.Clear();

        while (_undoStack.Count > MaxDepth)
            _undoStack.RemoveFirst();

        AppLogger.Debug("Executed: {Desc} (undo depth: {Depth})", command.Description, _undoStack.Count);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var command = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        command.Undo();
        _redoStack.AddLast(command);
        AppLogger.Debug("Undo: {Desc}", command.Description);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var command = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        command.Do();
        _undoStack.AddLast(command);
        AppLogger.Debug("Redo: {Desc}", command.Description);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
