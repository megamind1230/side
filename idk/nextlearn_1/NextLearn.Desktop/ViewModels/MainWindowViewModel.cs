using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;

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
    public void NavigateToHome()
    {
        IsLearning = false;
        CurrentView = HomeViewModel;
        HomeViewModel.Refresh();
    }

    [RelayCommand]
    public void NavigateToLearning(Guid deckId)
    {
        IsLearning = true;
        var deck = HomeViewModel.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck != null)
        {
            LearningViewModel.SetCurrentDeck(deck);
        }
        else
        {
            LearningViewModel.StartLearning(deckId);
        }
        CurrentView = LearningViewModel;
    }

    public void NavigateToLearningByDeck(Deck deck)
    {
        IsLearning = true;
        LearningViewModel.SetCurrentDeck(deck);
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
    public void ExitLearning()
    {
        IsLearning = false;
        NavigateToHome();
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

    private async void ClearStatusAfterDelay()
    {
        await Task.Delay(3000);
        SettingsStatus = "";
    }
}
