using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Services.Layout;

public class RadialLayout : ITreeLayoutAlgorithm
{
    public string Name => "Radial";

    public LayoutResult CalculateLayout(MindmapNode rootNode, double nodeWidth, double nodeHeight, double horizontalSpacing, double verticalSpacing)
    {
        var result = new LayoutResult();
        var radius = horizontalSpacing;

        //#baka root at center
        result.NodeLayouts[rootNode.Id] = new NodeLayout
        {
            NodeId = rootNode.Id, X = -nodeWidth / 2, Y = -nodeHeight / 2, Width = nodeWidth, Height = nodeHeight
        };

        var children = rootNode.Children.Where(c => !c.IsCollapsed).ToList();
        if (children.Count == 0) return result;

        //#baka distribute children in a semicircle below root
        var angleStep = Math.PI / (children.Count + 1);
        var startAngle = Math.PI; // bottom

        for (var i = 0; i < children.Count; i++)
        {
            var angle = startAngle + angleStep * (i + 1);
            var cx = radius * Math.Cos(angle);
            var cy = radius * Math.Sin(angle) + nodeHeight / 2;

            result.NodeLayouts[children[i].Id] = new NodeLayout
            {
                NodeId = children[i].Id, X = cx - nodeWidth / 2, Y = cy - nodeHeight / 2,
                Width = nodeWidth, Height = nodeHeight
            };

            //#baka recurse for grandchildren with increasing radius
            LayoutSubtree(children[i], cx, cy, radius + horizontalSpacing, angle, angleStep, result, nodeWidth, nodeHeight, horizontalSpacing);
        }

        return result;
    }

    private static void LayoutSubtree(MindmapNode node, double parentX, double parentY, double radius,
        double parentAngle, double angleStep, LayoutResult result, double nodeWidth, double nodeHeight, double spacing)
    {
        var grandchildren = node.Children.Where(c => !c.IsCollapsed).ToList();
        if (grandchildren.Count == 0) return;

        //#baka fan out grandchildren relative to parent angle
        var subStep = angleStep * 0.6;
        var startAngle = parentAngle - subStep * (grandchildren.Count - 1) / 2.0;

        for (var i = 0; i < grandchildren.Count; i++)
        {
            var angle = startAngle + subStep * i;
            var cx = radius * Math.Cos(angle);
            var cy = radius * Math.Sin(angle);

            result.NodeLayouts[grandchildren[i].Id] = new NodeLayout
            {
                NodeId = grandchildren[i].Id, X = cx - nodeWidth / 2, Y = cy - nodeHeight / 2,
                Width = nodeWidth, Height = nodeHeight
            };

            LayoutSubtree(grandchildren[i], cx, cy, radius + spacing, angle, subStep, result, nodeWidth, nodeHeight, spacing);
        }
    }
}
