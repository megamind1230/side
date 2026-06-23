using CommunityToolkit.Mvvm.ComponentModel;
using MindmapDotnet.Core.Models;

namespace MindmapDotnet.App.ViewModels;

public partial class NodeViewModel : ViewModelBase
{
    private readonly MindmapNode _model;

    public MindmapNode Model => _model;
    public Guid Id => _model.Id;
    public Guid? ParentId => _model.ParentId;

    public string Text
    {
        get => _model.Text;
        set => _model.Text = value;
    }

    public string Color
    {
        get => _model.Color;
        set => _model.Color = value;
    }

    public bool IsCollapsed
    {
        get => _model.IsCollapsed;
        set => _model.IsCollapsed = value;
    }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 140;

    [ObservableProperty]
    private double _height = 36;

    public NodeViewModel(MindmapNode model)
    {
        _model = model;
    }
}
