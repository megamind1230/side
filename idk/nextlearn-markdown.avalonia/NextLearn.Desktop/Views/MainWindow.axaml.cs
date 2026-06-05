using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NextLearn.Desktop.ViewModels;
using Serilog;

namespace NextLearn.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _chordPending;
    private IDisposable? _chordTimer;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void CancelChord()
    {
        _chordPending = false;
        _chordTimer?.Dispose();
        _chordTimer = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            if (vm.IsSettingsOpen)
            {
                vm.IsSettingsOpen = false;
                e.Handled = true;
                return;
            }
            if (vm.IsSidebarOpen)
            {
                vm.IsSidebarOpen = false;
                e.Handled = true;
                return;
            }
        }

        if ((e.Key == Key.D || e.Key == Key.Q) && vm.IsSettingsOpen)
        {
            vm.IsSettingsOpen = false;
            vm.NavigateToHomeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var focusedElement = this.FocusManager?.GetFocusedElement();
        bool isTextBox = focusedElement is TextBox;

        if (vm.IsLearning)
        {
            if (_chordPending) CancelChord();

            switch (e.Key)
            {
                case Key.N:
                case Key.Right:
                    if (!isTextBox)
                    {
                        vm.LearningViewModel.NextPageCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.P:
                case Key.Left:
                    if (!isTextBox)
                    {
                        vm.LearningViewModel.PreviousPageCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Q:
                case Key.D:
                    if (!isTextBox)
                    {
                        vm.NavigateToHomeCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.E:
                    if (!isTextBox)
                    {
                        OpenInSystemEditor();
                        e.Handled = true;
                    }
                    break;
                case Key.Oem2:
                    if (!isTextBox)
                    {
                        vm.LearningViewModel.ToggleSearchCommand.Execute(null);
                        DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("DeckSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    if (vm.LearningViewModel.ShowSearch)
                    {
                        vm.LearningViewModel.SearchText = "";
                        vm.LearningViewModel.ShowSearch = false;
                        e.Handled = true;
                    }
                    break;
                case Key.Enter:
                    if (vm.LearningViewModel.ShowSearch)
                    {
                        var searchText = vm.LearningViewModel.SearchText;
                        vm.LearningViewModel.ShowSearch = false;
                        OpenInSystemEditor(searchText);
                        e.Handled = true;
                    }
                    break;
            }
        }
        else
        {
            if (!isTextBox)
            {
                if (_chordPending && e.Key != Key.I && e.Key != Key.G)
                    CancelChord();

                switch (e.Key)
                {
                    case Key.G:
                        _chordPending = true;
                        _chordTimer = DispatcherTimer.RunOnce(() => CancelChord(), TimeSpan.FromMilliseconds(500));
                        e.Handled = true;
                        break;
                    case Key.I:
                        if (_chordPending)
                        {
                            vm.HomeViewModel.FocusSearch();
                            DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                            CancelChord();
                            e.Handled = true;
                            return;
                        }
                        break;
                    case Key.Q:
                    case Key.D:
                        vm.NavigateToHomeCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.J:
                    {
                        var sv = this.FindControl<ScrollViewer>("HomeScrollViewer");
                        if (sv != null) sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y + 40);
                        e.Handled = true;
                        break;
                    }
                    case Key.K:
                    {
                        var sv = this.FindControl<ScrollViewer>("HomeScrollViewer");
                        if (sv != null) sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y - 40);
                        e.Handled = true;
                        break;
                    }
                    case Key.Oem2:
                        DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.Escape)
            {
                FocusManager?.ClearFocus();
                e.Handled = true;
            }
        }
    }

    private void CloseSidebarOnBackdrop(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsSidebarOpen = false;
        }
    }

    private void OpenInSystemEditor(string? searchText = null)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.LearningViewModel.CurrentPage == null) return;

        var deckId = vm.LearningViewModel.CurrentPage.DeckId;
        var mdPath = vm.LearningViewModel.GetDeckMarkdownPath(deckId);

        if (!string.IsNullOrEmpty(mdPath))
        {
            vm.EditorLauncher.Open(mdPath, searchText);
        }
    }
}
