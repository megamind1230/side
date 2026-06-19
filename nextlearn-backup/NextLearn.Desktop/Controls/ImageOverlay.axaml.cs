using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Controls;

public partial class ImageOverlay : UserControl
{
    private Point _panAnchor;
    private bool _isPanning;
    private bool _pendingRecenter;
    private bool _centerOnNextLayout;
    private Size _preZoomExtent;
    private Vector _preZoomOffset;

    public ImageOverlay()
    {
        InitializeComponent();
        ImageOverlayScrollViewer.ScrollChanged += OnScrollViewerScrollChanged;

        if (DataContext is MainWindowViewModel vm)
        {
            SubscribeToVm(vm);
        }

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                SubscribeToVm(vm);
            }
        };
    }

    private void SubscribeToVm(MainWindowViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.ZoomLevel))
            {
                UpdateOverlayImageSize();
                RecenterScrollViewerAfterLayout(centerInViewport: false);
            }

            if (e.PropertyName == nameof(MainWindowViewModel.CurrentImageBitmap))
            {
                UpdateOverlayImageSize();
                RecenterScrollViewerAfterLayout(centerInViewport: true);
            }

            if (e.PropertyName == nameof(MainWindowViewModel.IsImageOverlayOpen) && vm.IsImageOverlayOpen)
            {
                UpdateOverlayImageSize();
                RecenterScrollViewerAfterLayout(centerInViewport: true);
            }
        };
    }

    private void CloseOnBackdrop(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseImageOverlayCommand.Execute(null);
        }
    }

    private void OnImageAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _panAnchor = e.GetPosition(ImageOverlayScrollViewer);
        e.Handled = true;
    }

    private void OnImageAreaMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || ImageOverlayScrollViewer == null)
        {
            return;
        }

        var pos = e.GetPosition(ImageOverlayScrollViewer);
        var dx = _panAnchor.X - pos.X;
        var dy = _panAnchor.Y - pos.Y;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1)
        {
            ImageOverlayScrollViewer.Offset = new Vector(
                ImageOverlayScrollViewer.Offset.X + dx,
                ImageOverlayScrollViewer.Offset.Y + dy);
            _panAnchor = pos;
        }
    }

    private void OnImageAreaReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
    }

    private void UpdateOverlayImageSize()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (OverlayImage?.Source == null)
        {
            return;
        }

        var maxDim = Math.Max(
            OverlayImage.Source.Size.Width,
            OverlayImage.Source.Size.Height) * vm.ZoomLevel;

        OverlayImage.Width = maxDim;
        OverlayImage.Height = maxDim;
    }

    private void RecenterScrollViewerAfterLayout(bool centerInViewport)
    {
        _pendingRecenter = true;
        _centerOnNextLayout = centerInViewport;
        if (!centerInViewport)
        {
            _preZoomExtent = ImageOverlayScrollViewer?.Extent ?? default;
            _preZoomOffset = ImageOverlayScrollViewer?.Offset ?? default;
        }
    }

    private void OnScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_pendingRecenter || e.ExtentDelta == default)
        {
            return;
        }

        _pendingRecenter = false;
        var ext = ImageOverlayScrollViewer.Extent;
        var vp = ImageOverlayScrollViewer.Viewport;
        if (ext.Width <= 0 || ext.Height <= 0)
        {
            return;
        }

        if (_centerOnNextLayout)
        {
            ImageOverlayScrollViewer.Offset = new Vector(
                Math.Max(0, (ext.Width - vp.Width) / 2),
                Math.Max(0, (ext.Height - vp.Height) / 2));
        }
        else
        {
            var scaleX = ext.Width / _preZoomExtent.Width;
            var scaleY = ext.Height / _preZoomExtent.Height;
            var newOffset = new Vector(
                ((_preZoomOffset.X + (vp.Width / 2)) * scaleX) - (vp.Width / 2),
                ((_preZoomOffset.Y + (vp.Height / 2)) * scaleY) - (vp.Height / 2));
            ImageOverlayScrollViewer.Offset = new Vector(
                Math.Clamp(newOffset.X, 0, Math.Max(0, ext.Width - vp.Width)),
                Math.Clamp(newOffset.Y, 0, Math.Max(0, ext.Height - vp.Height)));
        }
    }
}
