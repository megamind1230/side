using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Serilog;

// UI ViewModel — awaits must return to UI thread, no ConfigureAwait
#pragma warning disable CA2007
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using SkiaSharp;

namespace NextLearn.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly IDeckService _deckService;
    private readonly IDeckFileService _deckFileService;
    private readonly ISettingsService _settingsService;
    private readonly IKeyBindingService _keyBindingService;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _title = "NextLearn";

    [ObservableProperty]
    private bool _isLearning;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private Deck? _editingDeck;

    [ObservableProperty]
    private bool _isSidebarOpen;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isPinnedViewOpen;

    [ObservableProperty]
    private bool _isArchivedViewOpen;

    [ObservableProperty]
    private bool _isHeatmapOpen;

    [ObservableProperty]
    private int _todayMinutes;

    [ObservableProperty]
    private int _todayPages;

    [ObservableProperty]
    private int _todayDecks;

    [ObservableProperty]
    private int _todayStreak;

    [ObservableProperty]
    private ObservableCollection<HeatmapCell> _heatmapCells = new();

    [ObservableProperty]
    private double _heatmapCellScale = 1.0;

    [ObservableProperty]
    private bool _isMarketplaceOpen;

    [ObservableProperty]
    private bool _isFalconEyeEnabled;

    [ObservableProperty]
    private string _theme = string.Empty;

    [ObservableProperty]
    private string _font = string.Empty;

    [ObservableProperty]
    private string _decksPath = string.Empty;

    [ObservableProperty]
    private string _keyBindingsProfile = string.Empty;

    [ObservableProperty]
    private string _settingsStatus = string.Empty;

    [ObservableProperty]
    private bool _isShortcutsHandbookOpen;

    [ObservableProperty]
    private List<ShortcutSection> _handbookSections = new();

    [ObservableProperty]
    private bool _isImageOverlayOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentImageFileName))]
    private string _currentImagePath = string.Empty;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private double _textScale = 1.0;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _currentImageBitmap;

    private Avalonia.Media.Imaging.Bitmap? _normalBitmap;

    [ObservableProperty]
    private bool _isInverted;

    public Func<string?, Task<string?>>? PickFolderHandler { get; set; }

    public IKeyBindingService KeyBindingService => _keyBindingService;

    public string CurrentImageFileName => Path.GetFileName(CurrentImagePath);

    partial void OnCurrentImagePathChanged(string value)
    {
        if (string.IsNullOrEmpty(value) || !File.Exists(value))
        {
            CurrentImageBitmap = null;
            return;
        }

        var capturedPath = value;
        Task.Run(() =>
        {
#pragma warning disable CA1031
            try
            {
                var bytes = File.ReadAllBytes(capturedPath);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (CurrentImagePath != capturedPath) return;
                        var ms = new MemoryStream(bytes);
                        _normalBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                        CurrentImageBitmap = IsInverted
                            ? (CreateInvertedBitmap(_normalBitmap) ?? _normalBitmap)
                            : _normalBitmap;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to decode bitmap from {Path}", capturedPath);
                        if (CurrentImagePath == capturedPath)
                            CurrentImageBitmap = null;
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read image file {Path}", capturedPath);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (CurrentImagePath == capturedPath)
                        CurrentImageBitmap = null;
                });
            }
#pragma warning restore CA1031
        });
    }

    private static Avalonia.Media.Imaging.Bitmap? CreateInvertedBitmap(Avalonia.Media.Imaging.Bitmap source)
    {
        try
        {
            using var ms = new MemoryStream();
            source.Save(ms);
            ms.Position = 0;
            using var skData = SKData.Create(ms);
            using var skBitmap = SKBitmap.Decode(skData);
            if (skBitmap == null)
            {
                return null;
            }

            for (var y = 0; y < skBitmap.Height; y++)
            {
                for (var x = 0; x < skBitmap.Width; x++)
                {
                    var pixel = skBitmap.GetPixel(x, y);
                    skBitmap.SetPixel(x, y, new SKColor(
                        (byte)(255 - pixel.Red),
                        (byte)(255 - pixel.Green),
                        (byte)(255 - pixel.Blue),
                        pixel.Alpha));
                }
            }

            using var pngData = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var pngStream = pngData.AsStream();
            return new Avalonia.Media.Imaging.Bitmap(pngStream);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.Error(ex, "Failed to create inverted bitmap");
            return null;
        }
    }

    public HomeViewModel HomeViewModel { get; }

    public LearningViewModel LearningViewModel { get; }

    public MainWindowViewModel()
    {
        _context = new AppDbContext();
        _context.Database.EnsureCreated();
        MigrateSchema(_context);

        _userService = new UserService(_context);
        _deckService = new DeckService(_context, _userService);
        _deckFileService = new DeckFileService(_context);
        _settingsService = new SettingsService();
        _keyBindingService = new KeyBindingService();
        var htmlContentBuilder = new HtmlContentService();

        LoadSettings();

        if (!string.IsNullOrEmpty(_settingsService.KeyBindingsProfile))
        {
            _keyBindingService.SwitchProfile(_settingsService.KeyBindingsProfile);
        }

        KeyBindingsChanged += () =>
        {
            if (IsShortcutsHandbookOpen)
            {
                RebuildHandbookSections();
            }
        };

        var decksPath = _settingsService.ResolvedDecksPath;
        HomeViewModel = new HomeViewModel(_deckService, _deckFileService, this, decksPath);
        LearningViewModel = new LearningViewModel(_deckService, _userService, htmlContentBuilder, this, decksPath);

        CurrentView = HomeViewModel;
    }

#pragma warning disable CA1031
    private static void MigrateSchema(AppDbContext context)
    {
        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Decks ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Decks ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Pages DROP COLUMN DurationSeconds");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Pages DROP COLUMN MediaPath");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Decks DROP COLUMN Category");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Decks DROP COLUMN Difficulty");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Decks DROP COLUMN DownloadsCount");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Users DROP COLUMN Email");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Users DROP COLUMN PasswordHash");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Users DROP COLUMN TotalDecksShared");
        }
        catch
        {
        }

        try
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE UserProgress DROP COLUMN IsDownloaded");
        }
        catch
        {
        }
    }
#pragma warning restore CA1031

    private void LoadSettings()
    {
        Theme = _settingsService.Theme;
        Font = _settingsService.Font;
        DecksPath = _settingsService.DecksPath;
        KeyBindingsProfile = _settingsService.KeyBindingsProfile;
        IsFalconEyeEnabled = _settingsService.FalconEyeEnabled;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Theme = Theme;
        _settingsService.Font = Font;
        _settingsService.DecksPath = DecksPath;
        _settingsService.KeyBindingsProfile = KeyBindingsProfile;
        _settingsService.FalconEyeEnabled = IsFalconEyeEnabled;
        _keyBindingService.SwitchProfile(KeyBindingsProfile);
        KeyBindingsChanged?.Invoke();

        var resolved = _settingsService.ResolvedDecksPath;
        Directory.CreateDirectory(resolved);

        if (_settingsService.TrySave(out var error))
        {
            SettingsStatus = "Settings saved";
        }
        else
        {
            SettingsStatus = $"Error: {error}";
        }

        ClearStatusAfterDelay();

        var resolvedFont = string.IsNullOrWhiteSpace(Font) ? "Inter" : Font;
        FontChanged?.Invoke(resolvedFont);
    }

    [RelayCommand]
    private void ResetSettings()
    {
        var defaults = SettingsService.Defaults();
        Theme = defaults.Theme;
        Font = defaults.Font;
        DecksPath = defaults.DecksPath;
        KeyBindingsProfile = defaults.KeyBindingsProfile;
        IsFalconEyeEnabled = defaults.FalconEyeEnabled;
    }

    [RelayCommand]
    private async Task BrowseDecksPathAsync()
    {
        if (PickFolderHandler != null)
        {
            var result = await PickFolderHandler(DecksPath);
            if (result != null)
            {
                DecksPath = result;
            }
        }
    }

    [RelayCommand]
    private void OpenDecksFolder()
    {
        var path = _settingsService.ResolvedDecksPath;
        Log.Information("OpenDecksFolder: {Path}", path);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer", path);
                Log.Information("Opened via explorer");
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", path);
                Log.Information("Opened via open");
                return;
            }

            foreach (var fm in new[]
            {
                "thunar", "nautilus", "dolphin", "nemo", "caja",
                "pcmanfm", "konqueror", "krusader", "doublecmd",
                "spacefm", "xfe", "rox-filer",
                "ranger", "nnn", "lf", "mc", "yazi",
            })
            {
                try
                {
                    Process.Start(fm, path);
                    Log.Information("Opened via {FileManager}", fm);
                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    Log.Debug(ex, "{FileManager} not found for {Path}", fm, path);
                }
            }

            try
            {
                Process.Start("xdg-open", path);
                Log.Information("Opened via xdg-open (last resort)");
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                Log.Debug(ex, "xdg-open also failed for {Path}", path);
            }

            Log.Error("No file manager found to open {Path}", path);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open decks folder: {Path}", path);
        }
#pragma warning restore CA1031
    }

    [RelayCommand]
    public async Task NavigateToHomeAsync()
    {
        if (IsLearning)
        {
            await LearningViewModel.SaveProgressAsync();
        }

        IsLearning = false;
        CurrentView = HomeViewModel;
        HomeViewModel.Refresh();
    }

    [RelayCommand]
    public async Task NavigateToLearningAsync(Guid deckId)
    {
        IsPinnedViewOpen = false;
        IsArchivedViewOpen = false;
        IsLearning = true;
        var deck = HomeViewModel.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck != null)
        {
            await LearningViewModel.SetCurrentDeckAsync(deck);
        }
        else
        {
            LearningViewModel.StartLearning(deckId);
        }

        CurrentView = LearningViewModel;
    }

    public async void NavigateToLearningByDeck(Deck deck)
    {
        IsPinnedViewOpen = false;
        IsArchivedViewOpen = false;
        IsLearning = true;
        await LearningViewModel.SetCurrentDeckAsync(deck);
        CurrentView = LearningViewModel;
    }

    [RelayCommand]
    public void NavigateToMarketplace()
    {
        IsSidebarOpen = false;
        IsPinnedViewOpen = false;
        IsArchivedViewOpen = false;
        IsSettingsOpen = false;
        IsHeatmapOpen = false;
        IsMarketplaceOpen = true;
    }

    [RelayCommand]
    public void CloseMarketplace()
    {
        IsMarketplaceOpen = false;
    }

    [RelayCommand]
    public async Task ExitLearningAsync()
    {
        await NavigateToHomeAsync();
    }

    [RelayCommand]
#pragma warning disable CA1822
    public void NavigateToDocumentation()
#pragma warning restore CA1822
    {
        Views.MainWindow.OpenInBrowser("https://github.com/megamind1230/side/blob/master/nextlearn/README.org");
    }

    [RelayCommand]
#pragma warning disable CA1822
    public void NavigateToPlugins()
#pragma warning restore CA1822
    {
        Views.MainWindow.OpenInBrowser("https://github.com/megamind1230");
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    private void ToggleFalconEye()
    {
        IsFalconEyeEnabled = !IsFalconEyeEnabled;
        if (IsLearning)
        {
            LearningViewModel.RebuildWithFalconEye(IsFalconEyeEnabled);
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        LoadSettings();
        SettingsStatus = string.Empty;
        IsSidebarOpen = false;
        IsPinnedViewOpen = false;
        IsArchivedViewOpen = false;
        IsHeatmapOpen = false;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        SettingsStatus = string.Empty;
        IsSettingsOpen = false;
    }

    public ObservableCollection<Deck> PinnedDecks { get; set; } = new();

    public ObservableCollection<Deck> ArchivedDecks { get; set; } = new();

    [RelayCommand]
    public void ShowPinnedView()
    {
        var decksPath = _settingsService.ResolvedDecksPath;
        PinnedDecks.Clear();
        foreach (var d in DeckFileService.GetPinnedDecks(decksPath))
        {
            PinnedDecks.Add(d);
        }

        IsSidebarOpen = false;
        IsArchivedViewOpen = false;
        IsSettingsOpen = false;
        IsHeatmapOpen = false;
        IsPinnedViewOpen = true;
    }

    [RelayCommand]
    public void ShowArchivedView()
    {
        var decksPath = _settingsService.ResolvedDecksPath;
        ArchivedDecks.Clear();
        foreach (var d in DeckFileService.GetArchivedDecks(decksPath))
        {
            ArchivedDecks.Add(d);
        }

        IsSidebarOpen = false;
        IsPinnedViewOpen = false;
        IsSettingsOpen = false;
        IsHeatmapOpen = false;
        IsArchivedViewOpen = true;
    }

    [RelayCommand]
    public void ClosePinnedView()
    {
        IsPinnedViewOpen = false;
    }

    [RelayCommand]
    public void CloseArchivedView()
    {
        IsArchivedViewOpen = false;
    }

    [RelayCommand]
    public void UnpinFromView(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var decksPath = _settingsService.ResolvedDecksPath;
        _deckFileService.UnpinDeck(deck.Id, decksPath);
        PinnedDecks.Remove(deck);
        HomeViewModel.Refresh();
    }

    [RelayCommand]
    public void Unarchive(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var decksPath = _settingsService.ResolvedDecksPath;
        _deckFileService.UnarchiveDeck(deck.Id, decksPath);
        ArchivedDecks.Remove(deck);
        HomeViewModel.Refresh();
    }

    [RelayCommand]
    public void ShowHeatmap()
    {
        RefreshHeatmap();
        IsSidebarOpen = false;
        IsPinnedViewOpen = false;
        IsArchivedViewOpen = false;
        IsSettingsOpen = false;
        IsMarketplaceOpen = false;
        IsHeatmapOpen = true;
    }

    [RelayCommand]
    public void CloseHeatmap()
    {
        IsHeatmapOpen = false;
    }

    [RelayCommand]
    private void ZoomHeatmapIn()
    {
        HeatmapCellScale = Math.Min(3.0, HeatmapCellScale + 0.25);
    }

    [RelayCommand]
    private void ZoomHeatmapOut()
    {
        HeatmapCellScale = Math.Max(0.5, HeatmapCellScale - 0.25);
    }

    [RelayCommand]
    private void ZoomHeatmapReset()
    {
        HeatmapCellScale = 1.0;
    }

    private void RefreshHeatmap()
    {
        var (minutes, pages, decks, streak) = _userService.GetTodayStats();
        TodayMinutes = minutes;
        TodayPages = pages;
        TodayDecks = decks;
        TodayStreak = streak;

        var activity = _userService.GetActivityHistory(365);
        var activityByDate = activity.ToDictionary(a => a.Date, a => a.MinutesLearned);

        var today = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
        HeatmapCells.Clear();

        var year = today.Year;
        var start = new DateTime(year, 1, 1);
        var end = today;

        var current = start;
        while (current <= end)
        {
            var totalDays = (int)(current - start).TotalDays;
            var oldRow = totalDays / 7;
            var oldCol = (oldRow % 2 == 0) ? totalDays % 7 : 6 - (totalDays % 7);

            // Rotate 90° CCW so Jan 1 is at bottom-left, days snake upward
            const int maxOldCol = 6;
            var row = maxOldCol - oldCol;
            var col = oldRow;

            activityByDate.TryGetValue(current, out var minutesLearned);

            HeatmapCells.Add(new HeatmapCell
            {
                Date = current,
                Count = minutesLearned,
                Row = row,
                Col = col,
            });

            current = current.AddDays(1);
        }
    }

    partial void OnIsShortcutsHandbookOpenChanged(bool value)
    {
        if (value)
        {
            RebuildHandbookSections();
        }
    }

    [RelayCommand]
    public void CloseShortcutsHandbook()
    {
        IsShortcutsHandbookOpen = false;
    }

    public void RebuildHandbookSections()
    {
        var sections = new Dictionary<string, List<ShortcutEntry>>();

        foreach (var b in _keyBindingService.CurrentBindings)
        {
            var section = b.Context ?? "Global";
            if (section == "ImageOverlay")
            {
                section = "Image Overlay";
            }

            if (!sections.ContainsKey(section))
            {
                sections[section] = new List<ShortcutEntry>();
            }

            var keyText = FormatKeyForDisplay(b.Key, b.Modifiers);
            var desc = b.Comment ?? b.Action.ToString();

            sections[section].Add(new ShortcutEntry
            {
                Section = section,
                KeyText = keyText,
                Description = desc,
            });
        }

        // Add static Esc entry (not in binding table)
        sections["Global"].Add(new ShortcutEntry
        {
            Section = "Global",
            KeyText = "Esc",
            Description = "Close current overlay",
        });

        // Add chord info
        sections["Home"].Add(new ShortcutEntry
        {
            Section = "Home",
            KeyText = "g then i",
            Description = "Focus and clear search bar",
        });

        HandbookSections = sections
            .OrderBy(s => s.Key is "Global" ? 0 : s.Key is "Home" ? 1 : s.Key is "Learning" ? 2 : s.Key is "Image Overlay" ? 3 : 4)
            .Select(s => new ShortcutSection { Name = s.Key, Entries = s.Value })
            .ToList();
    }

    private static string FormatKeyForDisplay(string key, string modifiers)
    {
        var parts = new List<string>();
        if (modifiers.Contains("Control"))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.Contains("Shift"))
        {
            parts.Add("Shift");
        }

        if (modifiers.Contains("Alt"))
        {
            parts.Add("Alt");
        }

        parts.Add(key switch
        {
            "OemPlus" => "+",
            "OemMinus" => "-",
            "OemComma" => ",",
            "OemPeriod" => ".",
            "Oem2" => modifiers.Contains("Shift") ? "?" : "/",
            "D0" => "0",
            "D1" => "1",
            "D2" => "2",
            "D3" => "3",
            "D4" => "4",
            "D5" => "5",
            "D6" => "6",
            "D7" => "7",
            "D8" => "8",
            "D9" => "9",
            "NumPad0" => "0",
            "Left" => "←",
            "Right" => "→",
            "Up" => "↑",
            "Down" => "↓",
            "Space" => "Space",
            "Escape" => "Esc",
            _ => key.ToLowerInvariant(),
        });

        return string.Join(" + ", parts);
    }

    public void OpenImageOverlay(string imagePath)
    {
        try
        {
            var paths = LearningViewModel.CurrentPageImagePaths;
            var idx = paths.IndexOf(imagePath);
            CurrentImageIndex = idx >= 0 ? idx : 0;
            CurrentImagePath = imagePath;
            ZoomLevel = 1.0;
            IsImageOverlayOpen = true;
            Log.Information(
                "Image overlay opened: {Path} (index {Index} of {Count})",
                imagePath,
                CurrentImageIndex,
                paths.Count);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open image overlay for {Path}", imagePath);
        }
#pragma warning restore CA1031
    }

    [ObservableProperty]
    private int _currentImageIndex;

    [RelayCommand]
    public void CloseImageOverlay()
    {
        IsImageOverlayOpen = false;
        ZoomLevel = 1.0;
        Log.Information("Image overlay closed");
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(3.0, ZoomLevel + 0.25);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(0.5, ZoomLevel - 0.25);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
    }

    public event Action? KeyBindingsChanged;

    public event Action<double, double>? TextScaleChanged;

    public event Action<string>? FontChanged;

    [RelayCommand]
    private void ZoomTextIn()
    {
        var old = TextScale;
        TextScale = Math.Min(2.0, TextScale + 0.2);
        TextScaleChanged?.Invoke(old, TextScale);
    }

    [RelayCommand]
    private void ZoomTextOut()
    {
        var old = TextScale;
        TextScale = Math.Max(0.6, TextScale - 0.2);
        TextScaleChanged?.Invoke(old, TextScale);
    }

    [RelayCommand]
    private void ResetTextZoom()
    {
        var old = TextScale;
        TextScale = 1.0;
        TextScaleChanged?.Invoke(old, TextScale);
    }

    [RelayCommand]
    private void NextImage()
    {
        var paths = LearningViewModel.CurrentPageImagePaths;
        if (paths.Count == 0)
        {
            return;
        }

        CurrentImageIndex = (CurrentImageIndex + 1) % paths.Count;
        CurrentImagePath = paths[CurrentImageIndex];
        Log.Information("Image overlay next: {Path}", CurrentImagePath);
    }

    [RelayCommand]
    private void PreviousImage()
    {
        var paths = LearningViewModel.CurrentPageImagePaths;
        if (paths.Count == 0)
        {
            return;
        }

        CurrentImageIndex = (CurrentImageIndex - 1 + paths.Count) % paths.Count;
        CurrentImagePath = paths[CurrentImageIndex];
        Log.Information("Image overlay previous: {Path}", CurrentImagePath);
    }

    [RelayCommand]
    private void ToggleInvert()
    {
        IsInverted = !IsInverted;
        if (_normalBitmap == null)
        {
            return;
        }

        CurrentImageBitmap = IsInverted
            ? (CreateInvertedBitmap(_normalBitmap) ?? _normalBitmap)
            : _normalBitmap;
    }

    [RelayCommand]
    private void OpenImageInViewer()
    {
        if (string.IsNullOrEmpty(CurrentImagePath) || !File.Exists(CurrentImagePath))
        {
            return;
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(CurrentImagePath)
            {
                UseShellExecute = true,
            };
            process.Start();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open image in external viewer: {Path}", CurrentImagePath);
        }
#pragma warning restore CA1031
    }

    private async void ClearStatusAfterDelay()
    {
        await Task.Delay(3000);
        SettingsStatus = string.Empty;
    }
}
