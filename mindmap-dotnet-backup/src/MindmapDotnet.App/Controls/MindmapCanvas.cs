using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MindmapDotnet.App.ViewModels;
using MindmapDotnet.Core.Models;
using MindmapDotnet.Core.Services;

namespace MindmapDotnet.App.Controls;

public class MindmapCanvas : Control
{
    private MindmapService? _service;
    private List<NodeViewModel> _nodes = [];
    private Point _panOffset;
    private double _zoom = 1.0;
    private bool _isPanning;
    private Point _lastPanPos;

    public static readonly DirectProperty<MindmapCanvas, MindmapService?> ServiceProperty =
        AvaloniaProperty.RegisterDirect<MindmapCanvas, MindmapService?>(
            nameof(Service), o => o.Service, (o, v) => o.Service = v);

    public MindmapService? Service
    {
        get => _service;
        set
        {
            if (_service != null)
                _service.DocumentChanged -= OnDocChanged;
            _service = value;
            if (_service != null)
                _service.DocumentChanged += OnDocChanged;
            InvalidateVisual();
        }
    }

    public Action<NodeViewModel?>? NodeSelected { get; set; }
    public Action<NodeViewModel?>? NodeDoubleTapped { get; set; }
    public Action<KeyEventArgs>? KeyDownAction { get; set; }

    public void SetSelectedNode(Guid? id)
    {
        foreach (var node in _nodes)
            node.IsSelected = node.Id == id;
        InvalidateVisual();
    }

    public void SetNodes(List<NodeViewModel> nodes)
    {
        _nodes = nodes;
        InvalidateVisual();
    }

    private void OnDocChanged() => InvalidateVisual();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        KeyDownAction?.Invoke(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var adjusted = ScreenToCanvas(pos);

        NodeViewModel? hitNode = null;
        foreach (var node in _nodes)
        {
            var bounds = new Rect(node.X, node.Y, node.Width, node.Height);
            if (bounds.Contains(adjusted))
            {
                hitNode = node;
                break;
            }
        }

        foreach (var node in _nodes)
            node.IsSelected = node == hitNode;

        Focus();

        if (e.ClickCount == 2 && hitNode != null)
        {
            NodeDoubleTapped?.Invoke(hitNode);
        }
        else
        {
            NodeSelected?.Invoke(hitNode);
        }

        if (hitNode == null)
        {
            _isPanning = true;
            _lastPanPos = pos;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isPanning) return;

        var pos = e.GetPosition(this);
        var delta = pos - _lastPanPos;
        _panOffset = _panOffset + delta;
        _lastPanPos = pos;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPanning = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers != KeyModifiers.Control) return;

        var pos = e.GetPosition(this);
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom + e.Delta.Y * 0.1, 0.3, 3.0);

        var factor = _zoom / oldZoom;
        _panOffset = new Point(
            pos.X - factor * (pos.X - _panOffset.X),
            pos.Y - factor * (pos.Y - _panOffset.Y));

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_service == null || _nodes.Count == 0) return;

        var layout = _service.CalculateLayout(140, 36, 60, 20);
        foreach (var vm in _nodes)
        {
            if (layout.NodeLayouts.TryGetValue(vm.Id, out var nl))
            {
                vm.X = nl.X;
                vm.Y = nl.Y;
                vm.Width = nl.Width;
                vm.Height = nl.Height;
            }
        }

        var transform = Matrix.CreateTranslation(_panOffset.X, _panOffset.Y) *
                        Matrix.CreateScale(_zoom, _zoom);
        using var scope = context.PushTransform(transform);

        //#baka draw connector lines (bezier from parent right edge to child left edge)
        var lineBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
        foreach (var vm in _nodes)
        {
            if (vm.ParentId == null) continue;
            var parent = _nodes.FirstOrDefault(n => n.Id == vm.ParentId.Value);
            if (parent == null) continue;

            var startX = parent.X + parent.Width;
            var startY = parent.Y + parent.Height / 2;
            var endX = vm.X;
            var endY = vm.Y + vm.Height / 2;
            var midX = (startX + endX) / 2;

            var geo = new StreamGeometry();
            var geoCtx = geo.Open();
            geoCtx.BeginFigure(new Point(startX, startY), false);
            geoCtx.CubicBezierTo(
                new Point(midX, startY),
                new Point(midX, endY),
                new Point(endX, endY));
            geoCtx.EndFigure(false);
            geoCtx.Dispose();

            context.DrawGeometry(lineBrush, null, geo);
        }

        //#baka draw node rectangles with text
        foreach (var vm in _nodes)
        {
            var rect = new Rect(vm.X, vm.Y, vm.Width, vm.Height);
            var color = Color.Parse(vm.Color);

            context.FillRectangle(new SolidColorBrush(color), rect, 6f);

            if (vm.IsSelected)
            {
                var selRect = rect.Inflate(4);
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb(40, 249, 226, 175)),
                    selRect, 6f);
                context.DrawRectangle(
                    new Pen(new SolidColorBrush(Color.Parse("#f9e2af")), 3),
                    selRect, 6f);
            }

            var displayText = vm.Text.Length > 20 ? vm.Text[..17] + "..." : vm.Text;
            var formatted = new FormattedText(
                displayText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                13,
                Brushes.White);
            var textX = vm.X + 8;
            var textY = vm.Y + (vm.Height - formatted.Height) / 2;
            context.DrawText(formatted, new Point(textX, textY));

            //#baka collapse indicator (+ / -)
            if (_service != null)
            {
                var model = MindmapService.FindNode(_service.Document.RootNode, vm.Id);
                if (model?.Children.Count > 0)
                {
                    var indicator = vm.IsCollapsed ? "+" : "-";
                    var indFormatted = new FormattedText(
                        indicator,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Inter", weight: FontWeight.Bold),
                        14,
                        Brushes.White);
                    context.DrawText(indFormatted, new Point(
                        vm.X + vm.Width - indFormatted.Width - 6,
                        vm.Y + (vm.Height - indFormatted.Height) / 2));
                }
            }
        }
    }

    //#baka convert node's logical position to screen position (accounting for zoom/pan)
    public Point NodeToScreen(NodeViewModel vm)
    {
        return new Point(
            vm.X * _zoom + _panOffset.X,
            vm.Y * _zoom + _panOffset.Y);
    }

    public double Scale => _zoom;

    private Point ScreenToCanvas(Point screen)
    {
        return new Point(
            (screen.X - _panOffset.X) / _zoom,
            (screen.Y - _panOffset.Y) / _zoom);
    }
}
