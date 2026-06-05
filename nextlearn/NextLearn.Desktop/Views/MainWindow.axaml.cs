using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NativeWebView.Controls;
using NativeWebView.Core;
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

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnMainViewModelPropertyChanged;
            vm.LearningViewModel.PropertyChanged += OnLearningViewModelPropertyChanged;

            if (!string.IsNullOrEmpty(vm.LearningViewModel.RenderedHtml))
            {
                LoadHtmlContent(vm.LearningViewModel.RenderedHtml);
            }
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.LearningViewModel)
            && DataContext is MainWindowViewModel vm)
        {
            vm.LearningViewModel.PropertyChanged += OnLearningViewModelPropertyChanged;
        }
    }

    private void OnLearningViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LearningViewModel.RenderedHtml)
            && DataContext is MainWindowViewModel vm)
        {
            LoadHtmlContent(vm.LearningViewModel.RenderedHtml);
        }
    }

    private void LoadHtmlContent(string html)
    {
        try
        {
            if (ContentWebView == null) return;

            var appAssets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var cssPath = Path.Combine(appAssets, "atom-one-dark.min.css");
            var jsPath = Path.Combine(appAssets, "highlight.min.js");

            if (File.Exists(cssPath))
            {
                var css = File.ReadAllText(cssPath);
                html = html.Replace("<!--HIGHLIGHT_CSS-->", css);
            }
            else
            {
                html = html.Replace("<!--HIGHLIGHT_CSS-->", "");
            }

            if (File.Exists(jsPath))
            {
                var js = File.ReadAllText(jsPath);
                html = html.Replace("/* HIGHLIGHT_JS */", js);
            }
            else
            {
                html = html.Replace("<script>hljs.highlightAll();</script>", "");
                html = html.Replace("/* HIGHLIGHT_JS */", "");
            }

            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
            ContentWebView.Source = new Uri($"data:text/html;base64,{base64}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load HTML content into WebView");
        }
    }

    private void OnContentWebViewPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }

    private void OnContentWebViewGotFocus(object? sender, GotFocusEventArgs e)
    {
        Focus();
    }

    private void OnWebViewNavigationStarted(object? sender, NativeWebViewNavigationStartedEventArgs e)
    {
        var uri = e.Uri;
        if (uri == null) return;

        // Only intercept non-data, non-blank URIs (external links)
        if (uri.Scheme is "data" or "about") return;

        e.Cancel = true;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open external link {Uri}", uri);
        }
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
                case Key.J:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(0, 40)");
                        e.Handled = true;
                    }
                    break;
                case Key.K:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(0, -40)");
                        e.Handled = true;
                    }
                    break;
                case Key.H:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(-40,0)");
                        e.Handled = true;
                    }
                    break;
                case Key.L:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(40,0)");
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
