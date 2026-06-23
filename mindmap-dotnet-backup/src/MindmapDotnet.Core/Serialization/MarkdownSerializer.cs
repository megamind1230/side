using System.Text;
using System.Text.RegularExpressions;
using MindmapDotnet.Core.Models;

namespace MindmapDotnet.Core.Serialization;

public static partial class MarkdownSerializer
{
    private const string BulletPrefix = "* ";

    public static string Serialize(MindmapDocument doc)
    {
        var sb = new StringBuilder();
        SerializeNode(sb, doc.RootNode, 0);
        return sb.ToString().TrimEnd();
    }

    private static void SerializeNode(StringBuilder sb, MindmapNode node, int depth)
    {
        //#baka indent = 2 spaces per depth level, then "* " prefix
        var indent = new string(' ', depth * 2);
        sb.Append(indent);
        sb.Append(BulletPrefix);
        sb.AppendLine(node.Text);

        if (node.IsCollapsed) return;

        foreach (var child in node.Children)
            SerializeNode(sb, child, depth + 1);
    }

    public static MindmapDocument Deserialize(string markdown)
    {
        var doc = new MindmapDocument();
        var root = doc.RootNode;
        root.Text = "Root";

        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return doc;

        //#baka stack tracks parents at each depth level so we know where to attach
        var parentStack = new Stack<(MindmapNode Node, int Depth)>();
        parentStack.Push((root, -1));
        MindmapNode? lastNode = null;
        var lastDepth = -1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            //#baka count leading spaces to determine depth, then strip "* " prefix
            var leadingSpaces = LeadingSpacesRegex().Match(line).Length;
            var depth = leadingSpaces / 2;
            var content = line[leadingSpaces..];
            if (!content.StartsWith(BulletPrefix)) continue;
            var text = content[BulletPrefix.Length..];

            var node = new MindmapNode { Text = text };

            //#baka pop stack until we find parent at depth-1
            while (parentStack.Count > 0 && parentStack.Peek().Depth >= depth)
                parentStack.Pop();

            if (parentStack.Count > 0)
            {
                var parent = parentStack.Peek().Node;
                node.ParentId = parent.Id;
                parent.Children.Add(node);
            }

            parentStack.Push((node, depth));
            lastNode = node;
            lastDepth = depth;
        }

        return doc;
    }

    [GeneratedRegex(@"^ *")]
    private static partial Regex LeadingSpacesRegex();
}
