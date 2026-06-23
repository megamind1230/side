using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Services.Layout;

public class LeftToRightLayout : ITreeLayoutAlgorithm
{
    public string Name => "Left to Right";

    public LayoutResult CalculateLayout(MindmapNode rootNode, double nodeWidth, double nodeHeight, double horizontalSpacing, double verticalSpacing)
    {
        var result = new LayoutResult();
        //#baka root at (0,0), yOffset tracks vertical position as we recurse
        var yOffset = 0.0;
        LayoutNode(rootNode, 0, ref yOffset, result, nodeWidth, nodeHeight, horizontalSpacing, verticalSpacing);
        return result;
    }

    //#baka returns the total height used by this subtree (for sibling positioning)
    private static double LayoutNode(MindmapNode node, double x, ref double yOffset, LayoutResult result,
        double nodeWidth, double nodeHeight, double hSpacing, double vSpacing)
    {
        var children = node.Children.Where(c => !c.IsCollapsed).ToList();
        var startY = yOffset;

        if (children.Count == 0)
        {
            result.NodeLayouts[node.Id] = new NodeLayout
            {
                NodeId = node.Id, X = x, Y = yOffset, Width = nodeWidth, Height = nodeHeight
            };
            yOffset += nodeHeight + vSpacing;
            return nodeHeight + vSpacing;
        }

        var childX = x + nodeWidth + hSpacing;
        foreach (var child in children)
            LayoutNode(child, childX, ref yOffset, result, nodeWidth, nodeHeight, hSpacing, vSpacing);

        var endY = yOffset;
        var totalHeight = endY - startY;
        var centerY = startY + totalHeight / 2 - nodeHeight / 2;

        result.NodeLayouts[node.Id] = new NodeLayout
        {
            NodeId = node.Id, X = x, Y = centerY, Width = nodeWidth, Height = nodeHeight
        };

        return totalHeight;
    }
}
