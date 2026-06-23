namespace MindmapDotnet.Core.Commands;

public interface IUndoableCommand
{
    string Description { get; }
    void Do();
    void Undo();
}
