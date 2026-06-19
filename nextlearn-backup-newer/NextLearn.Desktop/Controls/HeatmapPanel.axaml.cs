using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Controls;

public partial class HeatmapPanel : UserControl
{
    private const double BaseCellSize = 20;
    private const double BaseCellGap = 3;
    private const double TopPadding = 4;
    private static readonly SolidColorBrush TodayBorder = new SolidColorBrush(Color.FromRgb(16, 185, 129));

    private double _cellScale = 1.0;

    public HeatmapPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            vm.HeatmapCells.CollectionChanged += OnCellsChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsHeatmapOpen) && vm.IsHeatmapOpen)
            {
                _cellScale = vm.HeatmapCellScale;
                RenderCells(vm.HeatmapCells);
            }

            if (e.PropertyName == nameof(MainWindowViewModel.HeatmapCellScale))
            {
                _cellScale = vm.HeatmapCellScale;
                RenderCells(vm.HeatmapCells);
            }
        }
    }

    private void OnCellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            RenderCells(vm.HeatmapCells);
        }
    }

    private void RenderCells(System.Collections.ObjectModel.ObservableCollection<HeatmapCell> cells)
    {
        HeatmapCanvas.Children.Clear();

        if (cells.Count == 0)
        {
            return;
        }

        var cellSize = BaseCellSize * _cellScale;
        var cellGap = BaseCellGap * _cellScale;
        var step = cellSize + cellGap;

        var today = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
        var maxCol = 0;
        var maxRow = 0;

        foreach (var cell in cells)
        {
            var isToday = cell.Date == today;

            var border = new Border
            {
                Width = cellSize,
                Height = cellSize,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(ColorFromHex(cell.Color)),
                Tag = cell,
            };

            if (isToday)
            {
                border.BorderBrush = TodayBorder;
                border.BorderThickness = new Thickness(1.5);
            }

            ToolTip.SetTip(border, cell.Tooltip);
            Canvas.SetLeft(border, cell.Col * step);
            Canvas.SetTop(border, TopPadding + (cell.Row * step));
            HeatmapCanvas.Children.Add(border);

            if (cell.Col > maxCol)
            {
                maxCol = cell.Col;
            }

            if (cell.Row > maxRow)
            {
                maxRow = cell.Row;
            }
        }

        HeatmapCanvas.Width = (maxCol + 1) * step;
        HeatmapCanvas.Height = TopPadding + ((maxRow + 1) * step);
    }

    private static Color ColorFromHex(string hex)
    {
        if (hex.Length == 7 && hex[0] == '#')
        {
            var r = Convert.ToByte(hex[1..3], 16);
            var g = Convert.ToByte(hex[3..5], 16);
            var b = Convert.ToByte(hex[5..7], 16);
            return Color.FromRgb(r, g, b);
        }

        return Colors.Transparent;
    }
}
