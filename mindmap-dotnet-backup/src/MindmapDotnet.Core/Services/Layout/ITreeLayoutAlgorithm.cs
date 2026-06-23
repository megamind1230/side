using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Services.Layout;

public interface ITreeLayoutAlgorithm
{
    string Name { get; }
    LayoutResult CalculateLayout(MindmapNode rootNode, double nodeWidth, double nodeHeight, double horizontalSpacing, double verticalSpacing);
}
