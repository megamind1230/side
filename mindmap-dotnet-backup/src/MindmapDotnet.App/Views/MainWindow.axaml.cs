using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MindmapDotnet.App.ViewModels;
using MindmapDotnet.Core.Logging;

namespace MindmapDotnet.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;
    private string _editOriginalText = string.Empty;
    private bool _giPending;
    private IDisposable? _giTimer;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        EditBox.KeyDown += OnEditBoxKeyDown;
        EditBox.LostFocus += (_, _) => CommitEdit();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as MainWindowViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsMarkdownView))
                    FocusCanvasOrEditor();
            };
            _vm.Service.DocumentChanged += SyncCanvas;
            Canvas.NodeSelected = vm =>
            {
                _vm.SelectedNode = vm;
                HideEditBox();
            };
            Canvas.NodeDoubleTapped = vm =>
            {
                _vm.SelectedNode = vm;
                _vm.BeginEditCommand.Execute(null);
            };
            Canvas.KeyDownAction = OnKeyDown;
            SyncCanvas();
            FocusCanvasOrEditor();
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private NodeViewModel? _trackedNode;

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.SelectedNode)) return;
        if (_trackedNode != null)
            _trackedNode.PropertyChanged -= OnSelectedNodePropertyChanged;
        _trackedNode = _vm?.SelectedNode;
        if (_trackedNode != null)
            _trackedNode.PropertyChanged += OnSelectedNodePropertyChanged;
        Canvas.SetSelectedNode(_vm?.SelectedNode?.Id);
    }

    private void OnSelectedNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NodeViewModel.IsEditing)) return;
        var node = _vm?.SelectedNode;
        if (node == null) return;

        if (node.IsEditing)
            ShowEditBox(node, selectAll: false);
        else
            HideEditBox();
    }

    private void SyncCanvas()
    {
        if (_vm == null) return;
        Dispatcher.UIThread.Post(() =>
        {
            Canvas.SetNodes([.. _vm.NodeViewModels]);
            Canvas.Service = _vm.Service;
            if (_vm.SelectedNode?.IsEditing == true)
                PositionEditBox(_vm.SelectedNode);
        });
    }

    private void FocusCanvasOrEditor()
    {
        if (_vm == null) return;
        if (_vm.IsMarkdownView)
            MarkdownEditor.Focus();
        else
            Canvas.Focus();
    }

    private void ShowEditBox(NodeViewModel node, bool selectAll)
    {
        _editOriginalText = node.Text;
        PositionEditBox(node);
        EditBox.Text = node.Text;
        EditBox.IsVisible = true;
        Dispatcher.UIThread.Post(() =>
        {
            EditBox.Focus();
            if (selectAll)
                EditBox.SelectAll();
            else
                EditBox.CaretIndex = EditBox.Text?.Length ?? 0;
        }, DispatcherPriority.Background);
    }

    private void PositionEditBox(NodeViewModel node)
    {
        if (_vm != null)
        {
            var layout = _vm.Service.CalculateLayout(140, 36, 60, 20);
            foreach (var vm in _vm.NodeViewModels)
            {
                if (layout.NodeLayouts.TryGetValue(vm.Id, out var nl))
                {
                    vm.X = nl.X;
                    vm.Y = nl.Y;
                }
            }
        }
        var pos = Canvas.NodeToScreen(node);
        var scale = Canvas.Scale;
        EditBox.Margin = new Thickness(pos.X, pos.Y, 0, 0);
        EditBox.Width = node.Width * scale;
        EditBox.Height = node.Height * scale;
        EditBox.FontSize = 13 * scale;
    }

    private void HideEditBox()
    {
        EditBox.IsVisible = false;
        Canvas.Focus();
    }

    private void CommitEdit()
    {
        if (_vm?.SelectedNode == null) return;
        if (!EditBox.IsVisible) return;

        _vm.SelectedNode.IsEditing = false;
        if (EditBox.Text != _editOriginalText)
            _vm.Service.EditText(_vm.SelectedNode.Model, EditBox.Text ?? "");
        HideEditBox();
    }

    private void OnEditBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitEdit();
                _vm?.InsertSiblingCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                CommitEdit();
                e.Handled = true;
                break;

            case Key.Tab:
                CommitEdit();
                _vm?.InsertChildCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (_vm == null) return;
        if (_vm.IsMarkdownView) return;

        //#baka handle `?` to toggle help overlay (works even while editing)
        if (e.Key == Key.OemQuestion)
        {
            HelpOverlay.IsVisible = !HelpOverlay.IsVisible;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && HelpOverlay.IsVisible)
        {
            HelpOverlay.IsVisible = false;
            e.Handled = true;
            return;
        }

        //#baka if editing, let the EditBox handle text input
        if (_vm.SelectedNode?.IsEditing == true) return;

        AppLogger.Debug("Key down: {Key}, Modifiers: {Mod}", e.Key, e.KeyModifiers);

        //#baka `g` prefix for gi chord
        if (e.Key == Key.G && e.KeyModifiers == KeyModifiers.None)
        {
            _giPending = true;
            _giTimer?.Dispose();
            _giTimer = DispatcherTimer.RunOnce(() => _giPending = false, TimeSpan.FromMilliseconds(500));
            e.Handled = false; // let it fall through
            return;
        }

        //#baka if g was pressed recently and now i → gi mode (overwrite)
        if (_giPending && e.Key == Key.I && e.KeyModifiers == KeyModifiers.None)
        {
            _giPending = false;
            _giTimer?.Dispose();
            if (_vm.SelectedNode != null)
            {
                _vm.SelectedNode.IsEditing = true;
                //#baka ShowEditBox will be called by OnSelectedNodePropertyChanged with selectAll=false,
                // but we need selectAll=true — so show it directly here
                ShowEditBox(_vm.SelectedNode, selectAll: true);
            }
            e.Handled = true;
            return;
        }
        _giPending = false;
        _giTimer?.Dispose();

        switch (e.Key)
        {
            case Key.I when e.KeyModifiers == KeyModifiers.None:
                _vm.BeginEditCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Tab when e.KeyModifiers == KeyModifiers.None:
                _vm.InsertChildCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                _vm.InsertSiblingCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                _vm.EndEditCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete:
            case Key.Back:
                _vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Z when e.KeyModifiers == KeyModifiers.Control:
                _vm.UndoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Y when e.KeyModifiers == KeyModifiers.Control:
                _vm.RedoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.M when e.KeyModifiers == KeyModifiers.Control:
                _vm.ToggleViewCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.L when e.KeyModifiers == KeyModifiers.Control:
                _vm.ToggleLayoutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left:
            case Key.H:
                NavigateParent();
                e.Handled = true;
                break;

            case Key.Right:
            case Key.L:
                NavigateChild();
                e.Handled = true;
                break;

            case Key.Up:
            case Key.K:
                NavigateSibling(-1);
                e.Handled = true;
                break;

            case Key.Down:
            case Key.J:
                NavigateSibling(1);
                e.Handled = true;
                break;
        }
    }

    private void NavigateParent()
    {
        if (_vm?.SelectedNode?.ParentId == null) return;
        _vm.SelectedNode = _vm.FindVm(_vm.SelectedNode.ParentId.Value);
    }

    private void NavigateChild()
    {
        if (_vm?.SelectedNode == null) return;
        var model = _vm.Service.Document.RootNode;
        var node = MindmapDotnet.Core.Services.MindmapService.FindNode(model, _vm.SelectedNode.Id);
        if (node?.Children.Count > 0)
            _vm.SelectedNode = _vm.FindVm(node.Children[0].Id);
    }

    private void NavigateSibling(int direction)
    {
        if (_vm?.SelectedNode == null) return;
        var sel = _vm.SelectedNode.Model;
        if (sel.ParentId == null) return;
        var parent = MindmapDotnet.Core.Services.MindmapService.FindNode(
            _vm.Service.Document.RootNode, sel.ParentId.Value);
        if (parent == null) return;
        var idx = parent.Children.IndexOf(sel);
        var newIdx = idx + direction;
        if (newIdx >= 0 && newIdx < parent.Children.Count)
            _vm.SelectedNode = _vm.FindVm(parent.Children[newIdx].Id);
    }
}
