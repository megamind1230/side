using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NativeWebView.Core;
using NextLearn.Desktop.Services;
using NextLearn.Desktop.ViewModels;
using Serilog;

// UI code-behind — exceptions in event handlers are caught and logged, no ConfigureAwait
#pragma warning disable CA1031
#pragma warning disable CA2007

namespace NextLearn.Desktop.Views;

public partial class MainWindow : Window
{
    private KeyboardHandler? _keyboardHandler;
    private WebViewBridge? _webViewBridge;
    private IDisposable? _chordTimer;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        AddHandler(
            InputElement.KeyDownEvent,
            (_, e) =>
            {
                if (e.Key != Key.O)
                {
                    return;
                }

                if (e.Source is TextBox)
                {
                    return;
                }

                if (DataContext is not MainWindowViewModel vm)
                {
                    return;
                }

                vm.OpenDecksFolderCommand.Execute(null);
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _keyboardHandler = new KeyboardHandler(vm);
            _webViewBridge = new WebViewBridge(ContentWebView);

            vm.PickFolderHandler = async (currentPath) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    return null;
                }

                var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Decks Directory",
                    SuggestedStartLocation = !string.IsNullOrEmpty(currentPath)
                        ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(
                            new Uri(currentPath.Replace("$HOME", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))))
                        : null,
                });

                return result.Count > 0 ? result[0].Path.LocalPath : null;
            };

            vm.PropertyChanged += OnMainViewModelPropertyChanged;
            vm.LearningViewModel.PropertyChanged += OnLearningViewModelPropertyChanged;
            vm.TextScaleChanged += OnTextScaleChanged;

            if (!string.IsNullOrEmpty(vm.LearningViewModel.RenderedHtml))
            {
                _webViewBridge.LoadHtml(vm.LearningViewModel.RenderedHtml);
            }
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || _webViewBridge == null)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.LearningViewModel))
        {
            vm.LearningViewModel.PropertyChanged += OnLearningViewModelPropertyChanged;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.IsImageOverlayOpen)
            or nameof(MainWindowViewModel.IsSidebarOpen)
            or nameof(MainWindowViewModel.IsSettingsOpen)
            or nameof(MainWindowViewModel.IsShortcutsHandbookOpen)
            or nameof(MainWindowViewModel.IsPinnedViewOpen)
            or nameof(MainWindowViewModel.IsArchivedViewOpen))
        {
            _webViewBridge.SetVisible(!(vm.IsImageOverlayOpen || vm.IsSidebarOpen || vm.IsSettingsOpen
                || vm.IsShortcutsHandbookOpen || vm.IsPinnedViewOpen || vm.IsArchivedViewOpen));
        }
    }

    private void OnLearningViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || _webViewBridge == null)
        {
            return;
        }

        if (e.PropertyName == nameof(LearningViewModel.RenderedHtml))
        {
            _webViewBridge.LoadHtml(vm.LearningViewModel.RenderedHtml);
        }

        if (e.PropertyName == nameof(LearningViewModel.IsGoToPageOpen))
        {
            _webViewBridge.SetVisible(!vm.LearningViewModel.IsGoToPageOpen);
        }
    }

    private void OnContentWebViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        Focus();
        Activate();
    }

    private void OnContentWebViewGotFocus(object? sender, GotFocusEventArgs e)
    {
        Focus();
        Activate();
    }

    private void OnWebViewNavigationStarted(object? sender, NativeWebViewNavigationStartedEventArgs e)
    {
        var uri = e.Uri;
        if (uri == null)
        {
            return;
        }

        if (uri.Scheme == "http" && uri.Host == "img.local")
        {
            e.Cancel = true;
            var result = WebViewBridge.DecodeImageUri(uri);
            if (result == null)
            {
                return;
            }

            var (path, _) = result.Value;
            if (path == null)
            {
                return;
            }

            Log.Information("Image clicked: {Path}", path);
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.OpenImageOverlay(path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to open image overlay for {Path}", path);
                }
            });
            return;
        }

        if (uri.Scheme == "http" && uri.Host == "key.local")
        {
            e.Cancel = true;
            var keyResult = WebViewBridge.DecodeKeyUri(uri);
            if (keyResult == null)
            {
                return;
            }

            var (key, mods) = keyResult.Value;
            Dispatcher.UIThread.Post(() => HandleKey(key, mods, false));
            return;
        }

        if (uri.Scheme == "http" && uri.Host == "openurl.local")
        {
            e.Cancel = true;
            var url = WebViewBridge.DecodeOpenUrl(uri);
            if (url != null)
            {
                OpenInBrowser(url);
            }

            return;
        }

        if (uri.Scheme is "data" or "about")
        {
            return;
        }

        e.Cancel = true;
        OpenInBrowser(uri.AbsoluteUri);
    }

    internal static void OpenInBrowser(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open URL on Windows: {Url}", url);
            }

            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            try
            {
                Process.Start("open", url);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open URL on macOS: {Url}", url);
            }

            return;
        }

        if (TryStartBrowser("firefox", url))
        {
            return;
        }

        var chromiumBrowsers = new[] { "brave", "google-chrome", "chromium-browser", "chromium" };
        foreach (var browser in chromiumBrowsers)
        {
            if (TryStartBrowser(browser, url, "--ozone-platform-hint=x11"))
            {
                return;
            }
        }

        try
        {
            Process.Start("xdg-open", url);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open URL via xdg-open: {Url}", url);
        }
    }

    private static bool TryStartBrowser(string binary, string url, string? extraArg = null)
    {
        try
        {
            var psi = new ProcessStartInfo(binary) { UseShellExecute = false };
            if (extraArg != null)
            {
                psi.ArgumentList.Add(extraArg);
            }

            psi.ArgumentList.Add(url);
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to launch browser {Binary}", binary);
            return false;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var focusedElement = this.FocusManager?.GetFocusedElement();
        bool isTextBox = focusedElement is TextBox;
        if (HandleKey(e.Key, e.KeyModifiers, isTextBox))
        {
            e.Handled = true;
        }
    }

    private bool HandleKey(Key key, KeyModifiers modifiers, bool isTextBox)
    {
        if (DataContext is not MainWindowViewModel vm || _keyboardHandler == null)
        {
            return false;
        }

        var action = _keyboardHandler.HandleKey(key, modifiers, isTextBox);

        if (action != KeyboardActionKind.ChordG && _chordTimer != null)
        {
            _chordTimer.Dispose();
            _chordTimer = null;
        }

        switch (action)
        {
            case KeyboardActionKind.None:
                return false;

            case KeyboardActionKind.ZoomTextIn:
                vm.ZoomTextInCommand.Execute(null);
                return true;

            case KeyboardActionKind.ZoomTextOut:
                vm.ZoomTextOutCommand.Execute(null);
                return true;

            case KeyboardActionKind.ResetTextZoom:
                vm.ResetTextZoomCommand.Execute(null);
                return true;

            case KeyboardActionKind.ZoomIn:
                vm.ZoomInCommand.Execute(null);
                return true;

            case KeyboardActionKind.ZoomOut:
                vm.ZoomOutCommand.Execute(null);
                return true;

            case KeyboardActionKind.ResetZoom:
                vm.ResetZoomCommand.Execute(null);
                return true;

            case KeyboardActionKind.NextImage:
                vm.NextImageCommand.Execute(null);
                return true;

            case KeyboardActionKind.PreviousImage:
                vm.PreviousImageCommand.Execute(null);
                return true;

            case KeyboardActionKind.CloseGoToPage:
                vm.LearningViewModel.CancelGoToPageCommand.Execute(null);
                return true;

            case KeyboardActionKind.CloseShortcutsHandbook:
                vm.IsShortcutsHandbookOpen = false;
                _webViewBridge?.SetVisible(true);
                return true;

            case KeyboardActionKind.CloseImageOverlay:
                vm.CloseImageOverlayCommand.Execute(null);
                return true;

            case KeyboardActionKind.CloseArchivedView:
                vm.CloseArchivedViewCommand.Execute(null);
                return true;

            case KeyboardActionKind.ClosePinnedView:
                vm.ClosePinnedViewCommand.Execute(null);
                return true;

            case KeyboardActionKind.CloseSettings:
                vm.IsSettingsOpen = false;
                return true;

            case KeyboardActionKind.CloseSidebar:
                vm.IsSidebarOpen = false;
                return true;

            case KeyboardActionKind.OpenSettings:
                vm.OpenSettingsCommand.Execute(null);
                return true;

            case KeyboardActionKind.ExitSettingsHome:
                vm.IsSettingsOpen = false;
                vm.NavigateToHomeCommand.Execute(null);
                return true;

            case KeyboardActionKind.ToggleShortcutsHandbook:
                vm.IsShortcutsHandbookOpen = !vm.IsShortcutsHandbookOpen;
                _webViewBridge?.SetVisible(!vm.IsShortcutsHandbookOpen);
                return true;

            case KeyboardActionKind.OpenGoToPage:
                vm.LearningViewModel.IsGoToPageOpen = true;
                return true;

            case KeyboardActionKind.NextPage:
                vm.LearningViewModel.NextPageCommand.Execute(null);
                return true;

            case KeyboardActionKind.PreviousPage:
                vm.LearningViewModel.PreviousPageCommand.Execute(null);
                return true;

            case KeyboardActionKind.ScrollDown:
                _webViewBridge?.ScrollBy(0, 40);
                return true;

            case KeyboardActionKind.ScrollUp:
                _webViewBridge?.ScrollBy(0, -40);
                return true;

            case KeyboardActionKind.ScrollLeft:
                _webViewBridge?.ScrollBy(-40, 0);
                return true;

            case KeyboardActionKind.ScrollRight:
                _webViewBridge?.ScrollBy(40, 0);
                return true;

            case KeyboardActionKind.NavigateHome:
                vm.NavigateToHomeCommand.Execute(null);
                return true;

            case KeyboardActionKind.ChordG:
                _chordTimer?.Dispose();
                _chordTimer = DispatcherTimer.RunOnce(() => _keyboardHandler.CancelChord(), TimeSpan.FromMilliseconds(500));
                return true;

            case KeyboardActionKind.FocusSearchWithClear:
                vm.HomeViewModel.FocusSearch();
                DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                return true;

            case KeyboardActionKind.FocusSearchBar:
                DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                return true;

            case KeyboardActionKind.ScrollDeckListDown:
            {
                var sv = this.FindControl<ScrollViewer>("HomeScrollViewer");
                if (sv != null)
                {
                    sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y + 40);
                }

                return true;
            }

            case KeyboardActionKind.ScrollDeckListUp:
            {
                var sv = this.FindControl<ScrollViewer>("HomeScrollViewer");
                if (sv != null)
                {
                    sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y - 40);
                }

                return true;
            }

            case KeyboardActionKind.ClearFocus:
                FocusManager?.ClearFocus();
                return true;

            default:
                return false;
        }
    }

    private async void OnTextScaleChanged(double oldScale, double newScale)
    {
        ApplyScaleToText(oldScale, newScale);
        _webViewBridge?.SetFontScale(newScale);
    }

    private void ApplyScaleToText(double oldScale, double newScale)
    {
        var ratio = newScale / oldScale;
        ScaleVisualFont(this, ratio);
    }

    private static void ScaleVisualFont(Control control, double ratio)
    {
        if (control is TextBlock tb)
        {
            tb.FontSize *= ratio;
        }

        if (control is TextBox tbx)
        {
            tbx.FontSize *= ratio;
        }

        foreach (var child in control.GetLogicalChildren())
        {
            if (child is Control c)
            {
                ScaleVisualFont(c, ratio);
            }
        }
    }
}
