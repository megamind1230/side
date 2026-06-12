using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using SkiaSharp;

namespace NextLearn.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly UserService _userService;
    private readonly DeckService _deckService;
    private readonly FlashcardService _flashcardService;
    private readonly SettingsService _settingsService;

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
    private string _editor = "";

    [ObservableProperty]
    private string _theme = "";

    [ObservableProperty]
    private string _font = "";

    [ObservableProperty]
    private string _decksPath = "";

    [ObservableProperty]
    private string _settingsStatus = "";

    [ObservableProperty]
    private bool _isShortcutsHandbookOpen;

    [ObservableProperty]
    private bool _isImageOverlayOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentImageFileName))]
    private string _currentImagePath = "";

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private int _rotationAngle;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _currentImageBitmap;

    private Avalonia.Media.Imaging.Bitmap? _normalBitmap;

    [ObservableProperty]
    private bool _isInverted;

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
        });
    }

    private Avalonia.Media.Imaging.Bitmap? CreateInvertedBitmap(Avalonia.Media.Imaging.Bitmap source)
    {
        try
        {
            using var ms = new MemoryStream();
            source.Save(ms);
            ms.Position = 0;
            var skData = SKData.Create(ms);
            using var skBitmap = SKBitmap.Decode(skData);
            if (skBitmap == null) return null;

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
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create inverted bitmap");
            return null;
        }
    }

    public HomeViewModel HomeViewModel { get; }
    public LearningViewModel LearningViewModel { get; }
    public FlashcardListViewModel FlashcardListViewModel { get; }
    public EditorViewModel EditorViewModel { get; }
    public EditorLauncher EditorLauncher { get; }

    public MainWindowViewModel()
    {
        _context = new AppDbContext();
        _context.Database.EnsureCreated();
        
        _userService = new UserService(_context);
        _deckService = new DeckService(_context, _userService);
        _flashcardService = new FlashcardService(_context, _userService);
        _settingsService = new SettingsService();

        LoadSettings();

        EditorLauncher = new EditorLauncher(_settingsService);

        var decksPath = _settingsService.ResolvedDecksPath;
        HomeViewModel = new HomeViewModel(_deckService, this, decksPath);
        LearningViewModel = new LearningViewModel(_deckService, _flashcardService, _userService, this, decksPath);
        FlashcardListViewModel = new FlashcardListViewModel(_flashcardService);
        EditorViewModel = new EditorViewModel(_deckService, this, decksPath);

        CurrentView = HomeViewModel;
    }

    private void LoadSettings()
    {
        Editor = _settingsService.Editor;
        Theme = _settingsService.Theme;
        Font = _settingsService.Font;
        DecksPath = _settingsService.DecksPath;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Editor = Editor;
        _settingsService.Theme = Theme;
        _settingsService.Font = Font;
        _settingsService.DecksPath = DecksPath;

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
    }

    [RelayCommand]
    private void ResetSettings()
    {
        var defaults = SettingsService.Defaults();
        Editor = defaults.Editor;
        Theme = defaults.Theme;
        Font = defaults.Font;
        DecksPath = defaults.DecksPath;
    }

    [RelayCommand]
    public void OpenEditor()
    {
        if (LearningViewModel.CurrentPage == null) return;
        
        var deckId = LearningViewModel.CurrentPage.DeckId;
        var deck = _deckService.GetDeckById(deckId);
        if (deck == null) return;

        EditingDeck = deck;
        EditorViewModel.LoadDeck(deck);
        IsEditorOpen = true;
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        EditingDeck = null;
    }

    [RelayCommand]
    public async Task NavigateToHome()
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
    public async Task NavigateToLearning(Guid deckId)
    {
        IsLearning = true;
        var deck = HomeViewModel.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck != null)
        {
            await LearningViewModel.SetCurrentDeck(deck);
        }
        else
        {
            LearningViewModel.StartLearning(deckId);
        }
        CurrentView = LearningViewModel;
    }

    public async void NavigateToLearningByDeck(Deck deck)
    {
        IsLearning = true;
        await LearningViewModel.SetCurrentDeck(deck);
        CurrentView = LearningViewModel;
    }

    [RelayCommand]
    public void NavigateToFlashcards()
    {
        IsLearning = false;
        FlashcardListViewModel.Refresh();
        CurrentView = FlashcardListViewModel;
    }

    [RelayCommand]
    public void NavigateToMarketplace()
    {
        Process.Start(new ProcessStartInfo("https://google.com") { UseShellExecute = true });
    }

    [RelayCommand]
    public async Task ExitLearning()
    {
        await NavigateToHome();
    }

    [RelayCommand]
    public void NavigateToPlugins()
    {
        Process.Start(new ProcessStartInfo("https://github.com/megamind1230") { UseShellExecute = true });
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        LoadSettings();
        SettingsStatus = "";
        IsSidebarOpen = false;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        SettingsStatus = "";
        IsSettingsOpen = false;
    }

    [RelayCommand]
    public void CloseShortcutsHandbook()
    {
        IsShortcutsHandbookOpen = false;
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
            RotationAngle = 0;
            IsImageOverlayOpen = true;
            Log.Information("Image overlay opened: {Path} (index {Index} of {Count})",
                imagePath, CurrentImageIndex, paths.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open image overlay for {Path}", imagePath);
        }
    }

    [ObservableProperty]
    private int _currentImageIndex;

    [RelayCommand]
    public void CloseImageOverlay()
    {
        IsImageOverlayOpen = false;
        ZoomLevel = 1.0;
        RotationAngle = 0;
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

    [RelayCommand]
    private void RotateCw()
    {
        RotationAngle = (RotationAngle + 90) % 360;
    }

    [RelayCommand]
    private void RotateCcw()
    {
        RotationAngle = (RotationAngle - 90 + 360) % 360;
    }

    [RelayCommand]
    private void NextImage()
    {
        var paths = LearningViewModel.CurrentPageImagePaths;
        if (paths.Count == 0) return;
        CurrentImageIndex = (CurrentImageIndex + 1) % paths.Count;
        CurrentImagePath = paths[CurrentImageIndex];
        Log.Information("Image overlay next: {Path}", CurrentImagePath);
    }

    [RelayCommand]
    private void PreviousImage()
    {
        var paths = LearningViewModel.CurrentPageImagePaths;
        if (paths.Count == 0) return;
        CurrentImageIndex = (CurrentImageIndex - 1 + paths.Count) % paths.Count;
        CurrentImagePath = paths[CurrentImageIndex];
        Log.Information("Image overlay previous: {Path}", CurrentImagePath);
    }

    [RelayCommand]
    private void ToggleInvert()
    {
        IsInverted = !IsInverted;
        if (_normalBitmap == null) return;
        CurrentImageBitmap = IsInverted
            ? (CreateInvertedBitmap(_normalBitmap) ?? _normalBitmap)
            : _normalBitmap;
    }

    [RelayCommand]
    private void OpenImageInViewer()
    {
        if (string.IsNullOrEmpty(CurrentImagePath) || !File.Exists(CurrentImagePath))
            return;
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(CurrentImagePath)
            {
                UseShellExecute = true
            };
            process.Start();
        }
        catch { }
    }

    private async void ClearStatusAfterDelay()
    {
        await Task.Delay(3000);
        SettingsStatus = "";
    }
}
