using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
                if (DataContext is not MainWindowViewModel vm)
                {
                    return;
                }

                // Open command palette before native WebView captures the key
                if (!vm.IsCommandPaletteOpen)
                {
                    if (vm.KeyBindingService.ActiveProfile == "Emacs")
                    {
                        // Emacs: M-x opens palette
                        if (e.Key == Key.X && e.KeyModifiers == KeyModifiers.Alt)
                        {
                            vm.OpenCommandPalette();
                            e.Handled = true;
                            return;
                        }
                    }
                    else
                    {
                        // Vim/Custom: : opens palette
                        if (e.Key == Key.OemSemicolon && e.KeyModifiers == KeyModifiers.Shift)
                        {
                            vm.OpenCommandPalette();
                            e.Handled = true;
                            return;
                        }
                    }
                }

                // o — open decks folder (Vim only, unmodified O)
                if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.None
                    && e.Source is not TextBox
                    && vm.KeyBindingService.ActiveProfile == "Vim")
                {
                    vm.OpenDecksFolderCommand.Execute(null);
                    e.Handled = true;
                }

                // Process ALL remaining keys through HandleKey before the WebView
                // consumes them (especially chords, Ctrl+V, Ctrl+B, Alt+V, etc.)
                if (!e.Handled && _keyboardHandler != null)
                {
                    var isTextBox = this.FocusManager?.GetFocusedElement() is TextBox;
                    if (HandleKey(e.Key, e.KeyModifiers, isTextBox))
                    {
                        e.Handled = true;
                    }
                }
            },
            RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _keyboardHandler = new KeyboardHandler(vm, vm.KeyBindingService);
            _webViewBridge = new WebViewBridge(ContentWebView);

            vm.KeyBindingsChanged += OnKeyBindingsChanged;

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
            vm.FontChanged += OnFontChanged;
            OnFontChanged(string.IsNullOrWhiteSpace(vm.Font) ? "Inter" : vm.Font);

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
            or nameof(MainWindowViewModel.IsArchivedViewOpen)
            or nameof(MainWindowViewModel.IsHeatmapOpen)
            or nameof(MainWindowViewModel.IsMarketplaceOpen)
            or nameof(MainWindowViewModel.IsCommandPaletteOpen))
        {
            _webViewBridge.SetVisible(!(vm.IsImageOverlayOpen || vm.IsSidebarOpen || vm.IsSettingsOpen
                || vm.IsShortcutsHandbookOpen || vm.IsPinnedViewOpen || vm.IsArchivedViewOpen
                || vm.IsHeatmapOpen || vm.IsMarketplaceOpen || vm.IsCommandPaletteOpen));
        }
    }

    private void OnKeyBindingsChanged()
    {
        _keyboardHandler?.RebuildLookup();
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

        if (action != KeyboardActionKind.ChordPrefix && _chordTimer != null)
        {
            _chordTimer.Dispose();
            _chordTimer = null;
        }

        // Update chord display
        if (action == KeyboardActionKind.ChordPrefix)
        {
            // Already handled in the case below — do nothing here
        }
        else if (action != KeyboardActionKind.None)
        {
            var completed = _keyboardHandler.LastCompletedChord;
            if (completed is { Count: > 0 })
            {
                vm.ChordDisplayText = FormatPendingChord(completed);
                DispatcherTimer.RunOnce(() => vm.ChordDisplayText = null, TimeSpan.FromSeconds(1));
            }
            else
            {
                vm.ChordDisplayText = null;
            }
        }
        else
        {
            vm.ChordDisplayText = null;
        }

        // Ctrl+Shift+= / Ctrl+Shift+- / Ctrl+Shift+0 — font-only zoom for entire app
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

            // Ctrl+= / Ctrl+- / Ctrl+0 — image overlay zoom
            case KeyboardActionKind.ZoomIn:
                vm.ZoomInCommand.Execute(null);
                return true;

            case KeyboardActionKind.ZoomOut:
                vm.ZoomOutCommand.Execute(null);
                return true;

            case KeyboardActionKind.ResetZoom:
                vm.ResetZoomCommand.Execute(null);
                return true;

            // Shift+N / Shift+P — cycle images in overlay
            case KeyboardActionKind.NextImage:
                vm.NextImageCommand.Execute(null);
                return true;

            case KeyboardActionKind.PreviousImage:
                vm.PreviousImageCommand.Execute(null);
                return true;

            // Esc context chain: GoToPage → Handbook → ImageOverlay → Archived → Pinned → Heatmap → Settings → Sidebar
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

            case KeyboardActionKind.CloseHeatmap:
                vm.CloseHeatmapCommand.Execute(null);
                return true;

            case KeyboardActionKind.CloseMarketplace:
                vm.CloseMarketplaceCommand.Execute(null);
                return true;

            // Ctrl+Shift+= / Ctrl+Shift+- / Ctrl+Shift+0 — heatmap zoom
            case KeyboardActionKind.ZoomHeatmapIn:
                vm.ZoomHeatmapInCommand.Execute(null);
                return true;

            case KeyboardActionKind.ZoomHeatmapOut:
                vm.ZoomHeatmapOutCommand.Execute(null);
                return true;

            case KeyboardActionKind.ZoomHeatmapReset:
                vm.ZoomHeatmapResetCommand.Execute(null);
                return true;

            // Esc or ←/Q/D from settings
            case KeyboardActionKind.CloseSettings:
                vm.IsSettingsOpen = false;
                return true;

            case KeyboardActionKind.CloseSidebar:
                vm.IsSidebarOpen = false;
                return true;

            // : / M-x — open command palette from anywhere
            case KeyboardActionKind.OpenCommandPalette:
                vm.OpenCommandPalette();
                return true;

            // Esc — close command palette
            case KeyboardActionKind.CloseCommandPalette:
                vm.CloseCommandPalette();
                return true;

            // Ctrl+, — open settings from anywhere
            case KeyboardActionKind.OpenSettings:
                vm.OpenSettingsCommand.Execute(null);
                return true;

            // Q/D — exit settings and go home
            case KeyboardActionKind.ExitSettingsHome:
                vm.IsSettingsOpen = false;
                vm.NavigateToHomeCommand.Execute(null);
                return true;

            // ? — toggle shortcuts handbook
            case KeyboardActionKind.ToggleShortcutsHandbook:
                vm.IsShortcutsHandbookOpen = !vm.IsShortcutsHandbookOpen;
                _webViewBridge?.SetVisible(!vm.IsShortcutsHandbookOpen);
                return true;

            // Ctrl+G — go to page number in study view
            case KeyboardActionKind.OpenGoToPage:
                vm.LearningViewModel.IsGoToPageOpen = true;
                return true;

            // N / Right arrow — next page; P / Left arrow — previous page
            case KeyboardActionKind.NextPage:
                vm.LearningViewModel.NextPageCommand.Execute(null);
                return true;

            case KeyboardActionKind.PreviousPage:
                vm.LearningViewModel.PreviousPageCommand.Execute(null);
                return true;

            // J / K / H / L — scroll WebView content in study view
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

            // Q / D / C-x q / C-x d — navigate home and close any open overlay
            case KeyboardActionKind.NavigateHome:
                vm.IsHeatmapOpen = false;
                vm.IsPinnedViewOpen = false;
                vm.IsArchivedViewOpen = false;
                vm.IsMarketplaceOpen = false;
                vm.IsSidebarOpen = false;
                vm.IsSettingsOpen = false;
                vm.NavigateToHomeCommand.Execute(null);
                return true;

            // Prefix of a multi-key chord — show pending, start/restart 500ms timeout
            case KeyboardActionKind.ChordPrefix:
                vm.ChordDisplayText = FormatPendingChord(_keyboardHandler.PendingChord);
                _chordTimer?.Dispose();
                Action cancelChord = () =>
                {
                    _keyboardHandler.CancelChord();
                    vm.ChordDisplayText = null;
                };
                _chordTimer = DispatcherTimer.RunOnce(cancelChord, TimeSpan.FromMilliseconds(500));
                return true;

            // g then i — focus search bar and clear current text
            case KeyboardActionKind.FocusSearchWithClear:
                vm.HomeViewModel.FocusSearch();
                DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                return true;

            // / — focus search bar without clearing
            case KeyboardActionKind.FocusSearchBar:
                DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                return true;

            // J / K — scroll deck list on home view
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

            // Esc — clear keyboard focus
            case KeyboardActionKind.ClearFocus:
                FocusManager?.ClearFocus();
                return true;

            // S — toggle sidebar
            case KeyboardActionKind.ToggleSidebar:
                vm.ToggleSidebarCommand.Execute(null);
                return true;

            // C-c o / O — open decks folder
            case KeyboardActionKind.OpenDecksFolder:
                vm.OpenDecksFolderCommand.Execute(null);
                return true;

            // C-x p — show pinned view
            case KeyboardActionKind.ShowPinnedView:
                vm.ShowPinnedViewCommand.Execute(null);
                return true;

            // C-x a — show archived view
            case KeyboardActionKind.ShowArchivedView:
                vm.ShowArchivedViewCommand.Execute(null);
                return true;

            // C-x h — show heatmap
            case KeyboardActionKind.ShowHeatmap:
                vm.ShowHeatmapCommand.Execute(null);
                return true;

            // C-c m — navigate to marketplace
            case KeyboardActionKind.NavigateToMarketplace:
                vm.NavigateToMarketplaceCommand.Execute(null);
                return true;

            // F1 — open documentation
            case KeyboardActionKind.OpenDocumentation:
                OpenInBrowser("https://github.com/megamind1230/side/blob/master/nextlearn/README.org");
                return true;

            default:
                return false;
        }
    }

    private static string FormatPendingChord(IReadOnlyList<(Key key, KeyModifiers modifiers)> chords)
    {
        var formatted = chords.Select(c => MainWindowViewModel.FormatKeyForDisplay(
            c.key.ToString(), ModifiersToString(c.modifiers), compact: true));
        return string.Join(" ", formatted);
    }

    private static string ModifiersToString(KeyModifiers mods)
    {
        var parts = new List<string>();
        if (mods.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Control");
        }

        if (mods.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (mods.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        return string.Join("+", parts);
    }

    private void OnFontChanged(string fontFamily)
    {
        FontFamily = new Avalonia.Media.FontFamily(fontFamily);
        _webViewBridge?.SetFontFamily(fontFamily);
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
