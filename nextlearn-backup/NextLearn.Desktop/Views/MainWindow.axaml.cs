using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private Point _panAnchor;
    private bool _isPanning;
    private bool _pendingRecenter;
    private bool _centerOnNextLayout;
    private Size _preZoomExtent;
    private Vector _preZoomOffset;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        ImageOverlayScrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
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
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.PropertyName == nameof(MainWindowViewModel.LearningViewModel))
        {
            vm.LearningViewModel.PropertyChanged += OnLearningViewModelPropertyChanged;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.IsImageOverlayOpen)
            or nameof(MainWindowViewModel.IsSidebarOpen)
            or nameof(MainWindowViewModel.IsSettingsOpen)
            or nameof(MainWindowViewModel.IsShortcutsHandbookOpen))
        {
            if (ContentWebView != null)
                ContentWebView.IsVisible = !(vm.IsImageOverlayOpen || vm.IsSidebarOpen || vm.IsSettingsOpen || vm.IsShortcutsHandbookOpen);
        }

        if (e.PropertyName is nameof(MainWindowViewModel.ZoomLevel)
            or nameof(MainWindowViewModel.RotationAngle))
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
        if (uri == null) return;

        // Intercept img.local HTTP requests → open floating overlay
        if (uri.Scheme == "http" && uri.Host == "img.local")
        {
            e.Cancel = true;
            var encodedPath = uri.AbsolutePath.TrimStart('/');
            byte[] pathBytes;
            string path;
            try
            {
                pathBytes = Convert.FromBase64String(encodedPath);
                path = System.Text.Encoding.UTF8.GetString(pathBytes);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to decode img.local URI");
                return;
            }
            Log.Information("Image clicked: {Path}", path);
            if (File.Exists(path))
            {
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
            }
            else
            {
                Log.Error("Image file not found: {Path}", path);
            }
            return;
        }

        // Intercept key.local HTTP requests → relay key to handleKey
        if (uri.Scheme == "http" && uri.Host == "key.local")
        {
            e.Cancel = true;
            var path = uri.AbsolutePath.TrimStart('/');
            var parts = path.Split('/');
            if (parts.Length >= 2)
            {
                try
                {
                    var keyStr = Uri.UnescapeDataString(parts[0]);
                    var modStr = parts[1];

                    Key key = keyStr switch
                    {
                        "n" or "N" => Key.N,
                        "p" or "P" => Key.P,
                        "j" or "J" => Key.J,
                        "k" or "K" => Key.K,
                        "h" or "H" => Key.H,
                        "l" or "L" => Key.L,
                        "q" or "Q" => Key.Q,
                        "d" or "D" => Key.D,
                        "e" or "E" => Key.E,
                        "i" or "I" => Key.I,
                        "g" or "G" => Key.G,
                        "Escape" => Key.Escape,
                        "?" => Key.Oem2,
                        "/" => Key.Oem2,
                        "Enter" => Key.Enter,
                        "," => Key.OemComma,
                        "=" => Key.OemPlus,
                        "-" => Key.OemMinus,
                        "ArrowRight" => Key.Right,
                        "ArrowLeft" => Key.Left,
                        _ => Key.None
                    };

                    if (key != Key.None)
                    {
                        var mods = KeyModifiers.None;
                        if (modStr.Contains('C')) mods |= KeyModifiers.Control;
                        if (modStr.Contains('S')) mods |= KeyModifiers.Shift;
                        if (modStr.Contains('A')) mods |= KeyModifiers.Alt;

                        Dispatcher.UIThread.Post(() => HandleKey(key, mods, false));
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to decode key.local URI: {Uri}", uri);
                }
            }
            return;
        }

        // Intercept openurl.local → open in system browser (from JS link click handler)
        if (uri.Scheme == "http" && uri.Host == "openurl.local")
        {
            e.Cancel = true;
            var path = uri.AbsolutePath.TrimStart('/');
            // Strip trailing /<timestamp> appended by the JS click handler (Date.now())
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                var lastSegment = path[(lastSlash + 1)..];
                if (lastSegment.All(char.IsDigit))
                    path = path[..lastSlash];
            }
            var url = Uri.UnescapeDataString(path);
            OpenInBrowser(url);
            return;
        }

        // Only intercept non-data, non-blank URIs (external links)
        if (uri.Scheme is "data" or "about") return;

        e.Cancel = true;
        OpenInBrowser(uri.AbsoluteUri);
    }

    private static void OpenInBrowser(string url)
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

        // Linux: Firefox first — no Wayland crash
        if (TryStartBrowser("firefox", url))
            return;

        // Chromium-family browsers crash on Wayland; force XWayland
        var chromiumBrowsers = new[] { "brave", "google-chrome", "chromium-browser", "chromium" };
        foreach (var browser in chromiumBrowsers)
        {
            if (TryStartBrowser(browser, url, "--ozone-platform-hint=x11"))
                return;
        }

        // Last resort: xdg-open
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
            if (extraArg != null) psi.ArgumentList.Add(extraArg);
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

    private void CancelChord()
    {
        _chordPending = false;
        _chordTimer?.Dispose();
        _chordTimer = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var focusedElement = this.FocusManager?.GetFocusedElement();
        bool isTextBox = focusedElement is TextBox;
        if (HandleKey(e.Key, e.KeyModifiers, isTextBox))
            e.Handled = true;
    }

    private bool HandleKey(Key key, KeyModifiers modifiers, bool isTextBox)
    {
        if (DataContext is not MainWindowViewModel vm) return false;

        // Image overlay keyboard shortcuts (checked before all others)
        if (vm.IsImageOverlayOpen)
        {
            if (key == Key.Escape) { vm.CloseImageOverlayCommand.Execute(null); return true; }
            if ((modifiers & KeyModifiers.Control) != 0 && key == Key.OemPlus) { vm.ZoomInCommand.Execute(null); return true; }
            if ((modifiers & KeyModifiers.Control) != 0 && key == Key.OemMinus) { vm.ZoomOutCommand.Execute(null); return true; }
            if ((modifiers & KeyModifiers.Control) != 0 && key is Key.D0 or Key.NumPad0) { vm.ResetZoomCommand.Execute(null); return true; }
            if (key == Key.N && modifiers.HasFlag(KeyModifiers.Shift)) { vm.NextImageCommand.Execute(null); return true; }
            if (key == Key.P && modifiers.HasFlag(KeyModifiers.Shift)) { vm.PreviousImageCommand.Execute(null); return true; }
        }

        if (key == Key.Escape)
        {
            if (vm.IsShortcutsHandbookOpen)
            {
                vm.IsShortcutsHandbookOpen = false;
                if (ContentWebView != null)
                    ContentWebView.IsVisible = true;
                return true;
            }
            if (vm.IsSettingsOpen) { vm.IsSettingsOpen = false; return true; }
            if (vm.IsSidebarOpen) { vm.IsSidebarOpen = false; return true; }
        }

        // Ctrl + , → Open Settings (global, works even in text boxes)
        if (key == Key.OemComma && modifiers.HasFlag(KeyModifiers.Control))
        {
            vm.OpenSettingsCommand.Execute(null);
            return true;
        }

        if ((key == Key.D || key == Key.Q) && vm.IsSettingsOpen)
        {
            vm.IsSettingsOpen = false;
            vm.NavigateToHomeCommand.Execute(null);
            return true;
        }

        // ? → Toggle Shortcuts Handbook
        if (key == Key.Oem2 && modifiers.HasFlag(KeyModifiers.Shift) && !isTextBox)
        {
            vm.IsShortcutsHandbookOpen = !vm.IsShortcutsHandbookOpen;
            if (ContentWebView != null)
                ContentWebView.IsVisible = !vm.IsShortcutsHandbookOpen;
            return true;
        }

        if (vm.IsLearning)
        {
            if (_chordPending) CancelChord();

            switch (key)
            {
                case Key.N:
                case Key.Right:
                    if (!isTextBox)
                    {
                        vm.LearningViewModel.NextPageCommand.Execute(null);
                        return true;
                    }
                    break;
                case Key.P:
                case Key.Left:
                    if (!isTextBox)
                    {
                        vm.LearningViewModel.PreviousPageCommand.Execute(null);
                        return true;
                    }
                    break;
                case Key.J:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(0, 40)");
                        return true;
                    }
                    break;
                case Key.K:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(0, -40)");
                        return true;
                    }
                    break;
                case Key.H:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(-40,0)");
                        return true;
                    }
                    break;
                case Key.L:
                    if (!isTextBox && ContentWebView != null)
                    {
                        _ = ContentWebView.ExecuteScriptAsync("window.scrollBy(40,0)");
                        return true;
                    }
                    break;
                case Key.Q:
                case Key.D:
                    if (!isTextBox)
                    {
                        vm.NavigateToHomeCommand.Execute(null);
                        return true;
                    }
                    break;
                case Key.E:
                    if (!isTextBox)
                    {
                        OpenInSystemEditor();
                        return true;
                    }
                    break;
                case Key.Oem2:
                    if (!isTextBox)
                    {
                        vm.LearningViewModel.ToggleSearchCommand.Execute(null);
                        DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("DeckSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                        return true;
                    }
                    break;
                case Key.Escape:
                    if (vm.LearningViewModel.ShowSearch)
                    {
                        vm.LearningViewModel.SearchText = "";
                        vm.LearningViewModel.ShowSearch = false;
                        return true;
                    }
                    break;
                case Key.Enter:
                    if (vm.LearningViewModel.ShowSearch)
                    {
                        var searchText = vm.LearningViewModel.SearchText;
                        vm.LearningViewModel.ShowSearch = false;
                        OpenInSystemEditor(searchText);
                        return true;
                    }
                    break;
            }
        }
        else
        {
            if (!isTextBox)
            {
                if (_chordPending && key != Key.I && key != Key.G)
                    CancelChord();

                switch (key)
                {
                    case Key.G:
                        _chordPending = true;
                        _chordTimer = DispatcherTimer.RunOnce(() => CancelChord(), TimeSpan.FromMilliseconds(500));
                        return true;
                    case Key.I:
                        if (_chordPending)
                        {
                            vm.HomeViewModel.FocusSearch();
                            DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                            CancelChord();
                            return true;
                        }
                        break;
                    case Key.Q:
                    case Key.D:
                        vm.NavigateToHomeCommand.Execute(null);
                        return true;
                    case Key.J:
                    {
                        var sv = this.FindControl<ScrollViewer>("HomeScrollViewer");
                        if (sv != null) sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y + 40);
                        return true;
                    }
                    case Key.K:
                    {
                        var sv = this.FindControl<ScrollViewer>("HomeScrollViewer");
                        if (sv != null) sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y - 40);
                        return true;
                    }
                    case Key.Oem2:
                        DispatcherTimer.RunOnce(() => this.FindControl<TextBox>("HomeSearchBox")?.Focus(), TimeSpan.FromMilliseconds(50));
                        return true;
                }
            }
            else if (key == Key.Escape)
            {
                FocusManager?.ClearFocus();
                return true;
            }
        }

        return false;
    }

    private void CloseSidebarOnBackdrop(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsSidebarOpen = false;
        }
    }

    private void CloseShortcutsHandbookOnBackdrop(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsShortcutsHandbookOpen = false;
            if (ContentWebView != null)
                ContentWebView.IsVisible = true;
        }
    }

    private void OnImageAreaPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _isPanning = true;
        _panAnchor = e.GetPosition(ImageOverlayScrollViewer);
        e.Handled = true;
    }

    private void OnImageAreaMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_isPanning || ImageOverlayScrollViewer == null) return;
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

    private void OnImageAreaReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _isPanning = false;
    }

    private void UpdateOverlayImageSize()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (OverlayImage?.Source == null) return;

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
            _preZoomExtent = ImageOverlayScrollViewer?.Extent ?? new Size();
            _preZoomOffset = ImageOverlayScrollViewer?.Offset ?? new Vector();
        }
    }

    private void OnScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_pendingRecenter || e.ExtentDelta == default) return;
        _pendingRecenter = false;
        var ext = ImageOverlayScrollViewer.Extent;
        var vp = ImageOverlayScrollViewer.Viewport;
        if (ext.Width <= 0 || ext.Height <= 0) return;

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
                (_preZoomOffset.X + vp.Width / 2) * scaleX - vp.Width / 2,
                (_preZoomOffset.Y + vp.Height / 2) * scaleY - vp.Height / 2);
            ImageOverlayScrollViewer.Offset = new Vector(
                Math.Clamp(newOffset.X, 0, Math.Max(0, ext.Width - vp.Width)),
                Math.Clamp(newOffset.Y, 0, Math.Max(0, ext.Height - vp.Height)));
        }
    }

    private void CloseImageOverlayOnBackdrop(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseImageOverlayCommand.Execute(null);
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
