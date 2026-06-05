using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NextLearn.ViewModels;

namespace NextLearn.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var focusedElement = this.FocusManager?.GetFocusedElement();
        bool isTextBox = focusedElement is TextBox;

        if (vm.IsLearning)
        {
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
                switch (e.Key)
                {
                    case Key.Q:
                    case Key.D:
                        vm.NavigateToHomeCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Oem2:
                        vm.ShowHomeSearchCommand.Execute(null);
                        DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                if (e.Key == Key.Escape && vm.IsHomeSearchOpen)
                {
                    vm.HomeViewModel.SearchText = "";
                    vm.CloseHomeSearchCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && vm.IsHomeSearchOpen)
                {
                    vm.CloseHomeSearchCommand.Execute(null);
                    e.Handled = true;
                }
            }
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
            try
            {
                string args;
                if (!string.IsNullOrEmpty(searchText))
                {
                    args = $"+/{searchText} \"{mdPath}\"";
                }
                else
                {
                    args = $"\"{mdPath}\"";
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "nvim",
                    Arguments = args,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                
                Process.Start(startInfo);
            }
            catch { }
        }
    }
}
