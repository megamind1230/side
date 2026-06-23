using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindmapDotnet.Core.Logging;
using MindmapDotnet.Core.Services;
using MindmapDotnet.Core.Services.Layout;

namespace MindmapDotnet.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly MindmapService _service = new();

    [ObservableProperty]
    private string _nodeCount = "0 nodes";

    [ObservableProperty]
    private string _title = "MindmapDotnet — Untitled";

    [ObservableProperty]
    private string _layoutMode = "LTR";

    [ObservableProperty]
    private bool _isMarkdownView;

    public bool IsMindmapView => !IsMarkdownView;

    partial void OnIsMarkdownViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMindmapView));
    }

    [ObservableProperty]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    public ObservableCollection<NodeViewModel> NodeViewModels { get; } = [];
    public MindmapService Service => _service;

    public MainWindowViewModel()
    {
        _service.DocumentChanged += OnDocumentChanged;
        _service.LayoutChanged += OnLayoutChanged;
        SetupDemoData();
        RebuildViewModels();
    }

    private void SetupDemoData()
    {
        _service.Document.RootNode.Text = "";
    }

    private void OnDocumentChanged()
    {
        RebuildViewModels();
        var count = _service.Document.NodeCount;
        NodeCount = $"{count} node{(count != 1 ? "s" : "")}";
        var file = _service.Document.FilePath;
        Title = $"MindmapDotnet — {(file != null ? Path.GetFileName(file) : "Untitled")}";
    }

    private void OnLayoutChanged()
    {
        LayoutMode = _service.LayoutName switch
        {
            "Left to Right" => "LTR",
            "Radial" => "Radial",
            _ => "LTR"
        };
    }

    public void RebuildViewModels()
    {
        AppLogger.Debug("Rebuilding view models");
        NodeViewModels.Clear();
        var allNodes = _service.GetAllNodes(_service.Document.RootNode);
        foreach (var node in allNodes)
            NodeViewModels.Add(new NodeViewModel(node));
    }

    public NodeViewModel? FindVm(Guid id) =>
        NodeViewModels.FirstOrDefault(vm => vm.Id == id);

    [RelayCommand]
    private void ToggleView()
    {
        if (IsMarkdownView)
        {
            try
            {
                var doc = Core.Serialization.MarkdownSerializer.Deserialize(MarkdownText);
                _service.Document.RootNode = doc.RootNode;
                _service.Document.IsModified = true;
                _service.Document.FilePath = doc.FilePath ?? _service.Document.FilePath;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Failed to parse markdown");
                return;
            }
            IsMarkdownView = false;
        }
        else
        {
            MarkdownText = Core.Serialization.MarkdownSerializer.Serialize(_service.Document);
            IsMarkdownView = true;
        }
    }

    [RelayCommand]
    private void ToggleLayout()
    {
        if (_service.LayoutName == "Left to Right")
            _service.SetLayout(new RadialLayout());
        else
            _service.SetLayout(new LeftToRightLayout());
    }

    [RelayCommand]
    private void Undo() => _service.Undo();
    [RelayCommand]
    private void Redo() => _service.Redo();

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedNode == null) return;

        var model = SelectedNode.Model;

        //#baka delete on root node clears everything and enters insert mode
        if (model == _service.Document.RootNode)
        {
            model.Children.Clear();
            model.Text = "";
            _service.Document.IsModified = true;
            RebuildViewModels();
            SelectedNode = FindVm(model.Id);
            if (SelectedNode != null)
                SelectedNode.IsEditing = true;
            return;
        }

        var parentId = model.ParentId;
        int? siblingIndex = null;

        // capture sibling index before deletion
        if (parentId != null)
        {
            var parent = MindmapService.FindNode(_service.Document.RootNode, parentId.Value);
            if (parent != null)
            {
                siblingIndex = parent.Children.IndexOf(model);
            }
        }

        _service.DeleteNode(model);

        // select next sibling, previous sibling, or parent
        if (parentId != null && siblingIndex >= 0)
        {
            var parent = MindmapService.FindNode(_service.Document.RootNode, parentId.Value);
            if (parent != null)
            {
                if (siblingIndex < parent.Children.Count)
                    SelectedNode = FindVm(parent.Children[siblingIndex.Value].Id);
                else if (siblingIndex > 0)
                    SelectedNode = FindVm(parent.Children[siblingIndex.Value - 1].Id);
                else
                    SelectedNode = FindVm(parent.Id);
            }
        }
        else
        {
            SelectedNode = null;
        }
    }

    [RelayCommand]
    private void InsertChild()
    {
        var parent = SelectedNode?.Model ?? _service.Document.RootNode;
        _service.InsertNode(parent, "");
        var newModel = parent.Children[^1];
        SelectedNode = FindVm(newModel.Id);
        if (SelectedNode != null)
            SelectedNode.IsEditing = true;
    }

    [RelayCommand]
    private void InsertSibling()
    {
        var sibling = SelectedNode?.Model;
        if (sibling == null || sibling == _service.Document.RootNode) return;
        _service.InsertSibling(sibling, "");
        var parent = MindmapService.FindNode(_service.Document.RootNode, sibling.ParentId ?? Guid.Empty);
        if (parent == null) return;
        var idx = parent.Children.IndexOf(sibling) + 1;
        if (idx < parent.Children.Count)
        {
            SelectedNode = FindVm(parent.Children[idx].Id);
            if (SelectedNode != null)
                SelectedNode.IsEditing = true;
        }
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedNode != null)
            SelectedNode.IsEditing = true;
    }

    [RelayCommand]
    private void EndEdit()
    {
        if (SelectedNode == null) return;
        SelectedNode.IsEditing = false;
        _service.EditText(SelectedNode.Model, SelectedNode.Text);
    }
}
