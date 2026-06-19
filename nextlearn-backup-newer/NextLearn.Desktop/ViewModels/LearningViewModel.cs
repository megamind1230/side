using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;

// UI ViewModel — awaits must return to UI thread, no ConfigureAwait
#pragma warning disable CA2007

namespace NextLearn.Desktop.ViewModels;

public partial class LearningViewModel : ViewModelBase
{
    private readonly IDeckService _deckService;
    private readonly IUserService _userService;
    private readonly IHtmlContentBuilder _htmlContentBuilder;
    private readonly MainWindowViewModel _mainViewModel;
    private readonly string _decksPath;

    private Deck? _currentDeck;
    private List<Page> _pages = new();
    private UserProgress? _progress;
    private Timer? _sessionTimer;
    private DateTime _sessionStartTime;
    private int _recordedMinutes;

    [ObservableProperty]
    private int _currentPageIndex;

    [ObservableProperty]
    private Page? _currentPage;

    [ObservableProperty]
    private string _currentSectionTitle = string.Empty;

    [ObservableProperty]
    private string _currentSectionDisplay = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSection))]
    private string _currentSectionBreadcrumb = string.Empty;

    [ObservableProperty]
    private string _currentPageBreadcrumb = string.Empty;

    [ObservableProperty]
    private string _renderedHtml = string.Empty;

    [ObservableProperty]
    private bool _isOrgFile;

    public bool HasSection => !string.IsNullOrEmpty(CurrentSectionBreadcrumb);

    [ObservableProperty]
    private string _deckTitle = string.Empty;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _progressText = "1 / 10";

    [ObservableProperty]
    private string _nextButtonText = "Next →";

    [ObservableProperty]
    private string _nextButtonColor = "#2563EB";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private List<string> _currentPageImagePaths = new();

    [ObservableProperty]
    private bool _isGoToPageOpen;

    [ObservableProperty]
    private string _goToPageInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGoToPageError))]
    private string? _goToPageError;

    public bool HasGoToPageError => !string.IsNullOrEmpty(GoToPageError);

    public LearningViewModel(
        IDeckService deckService,
        IUserService userService,
        IHtmlContentBuilder htmlContentBuilder,
        MainWindowViewModel mainViewModel,
        string? decksPath = null)
    {
        _deckService = deckService;
        _userService = userService;
        _htmlContentBuilder = htmlContentBuilder;
        _mainViewModel = mainViewModel;
        _decksPath = Constants.GetDecksPath(decksPath);
    }

    public async Task SetCurrentDeckAsync(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        _currentDeck = deck;
        _pages = deck.Pages.OrderBy(p => p.PageNumber).ToList();
        TotalPages = _pages.Count;
        DeckTitle = deck.HasExplicitTitle ? deck.Title : deck.FileName;
        IsOrgFile = deck.FileName?.EndsWith(".org", StringComparison.OrdinalIgnoreCase) ?? false;

        _progress = await _deckService.GetUserProgressAsync(deck.Id);
        if (_progress == null)
        {
            _progress = await _deckService.StartLearningAsync(deck.Id);
        }

        if (_progress.CurrentPage > 0)
        {
            CurrentPageIndex = _progress.CurrentPage - 1;
            if (CurrentPageIndex >= _pages.Count)
            {
                CurrentPageIndex = _pages.Count - 1;
            }
        }
        else
        {
            CurrentPageIndex = 0;
        }

        StartSessionTimer();
        UpdateCurrentPage();
    }

    public async Task SaveProgressAsync()
    {
        await StopSessionTimerAsync();
        if (_currentDeck != null)
        {
            await _deckService.UpdateProgressAsync(_currentDeck.Id, CurrentPageIndex + 1);
        }
    }

    private void StartSessionTimer()
    {
        _sessionStartTime = DateTime.UtcNow;
        _recordedMinutes = 0;
        _sessionTimer = new Timer(
            async _ =>
            {
                _recordedMinutes++;
                await _userService.RecordTimeAsync(1);
            },
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    private async Task StopSessionTimerAsync()
    {
        _sessionTimer?.Dispose();
        _sessionTimer = null;
        var totalSeconds = (int)(DateTime.UtcNow - _sessionStartTime).TotalSeconds;
        var totalMinutes = totalSeconds > 0 ? (totalSeconds + 59) / 60 : 0;
        var remaining = totalMinutes - _recordedMinutes;
        if (remaining > 0)
        {
            await _userService.RecordTimeAsync(remaining);
        }
    }

    public async void StartLearning(Guid deckId)
    {
        if (!Directory.Exists(_decksPath))
        {
            Directory.CreateDirectory(_decksPath);
            return;
        }

        var extensions = new[] { "*.md", "*.org" };
        Deck? deck = null;

        foreach (var ext in extensions)
        {
            var files = Directory.GetFiles(_decksPath, ext);
            foreach (var file in files)
            {
                var loadedDeck = DeckFileParser.LoadDeckFromFile(file);
                if (loadedDeck != null && loadedDeck.Id == deckId)
                {
                    deck = loadedDeck;
                    break;
                }
            }

            if (deck != null)
            {
                break;
            }
        }

        if (deck == null)
        {
            var dbDeck = _deckService.GetDeckById(deckId);
            if (dbDeck != null)
            {
                deck = dbDeck;
            }
            else
            {
                return;
            }
        }

        _currentDeck = deck;
        _pages = deck.Pages.OrderBy(p => p.PageNumber).ToList();
        TotalPages = _pages.Count;
        DeckTitle = deck.HasExplicitTitle ? deck.Title : deck.FileName;
        IsOrgFile = deck.FileName?.EndsWith(".org", StringComparison.OrdinalIgnoreCase) ?? false;

        _progress = await _deckService.GetUserProgressAsync(deckId);
        if (_progress == null)
        {
            _progress = await _deckService.StartLearningAsync(deckId);
        }

        CurrentPageIndex = _progress.CurrentPage - 1;
        if (CurrentPageIndex < 0)
        {
            CurrentPageIndex = 0;
        }

        if (CurrentPageIndex >= _pages.Count)
        {
            CurrentPageIndex = _pages.Count - 1;
        }

        UpdateCurrentPage();
    }

    private void UpdateCurrentPage()
    {
        if (_pages.Count == 0)
        {
            return;
        }

        CurrentPage = _pages[CurrentPageIndex];
        CurrentSectionTitle = CurrentPage?.SectionTitle ?? string.Empty;
        CurrentSectionDisplay = !string.IsNullOrEmpty(CurrentSectionTitle) ? $" → {CurrentSectionTitle}" : string.Empty;
        ProgressText = $"{CurrentPageIndex + 1} / {TotalPages}";
        CanGoBack = CurrentPageIndex > 0;
        CanGoNext = CurrentPageIndex < TotalPages - 1;
        IsCompleted = false;

        if (!string.IsNullOrEmpty(CurrentSectionTitle))
        {
            var h1Marker = IsOrgFile ? "*" : "#";
            CurrentSectionBreadcrumb = $"{h1Marker} {CurrentSectionTitle}";
        }
        else if (CurrentPage?.IsPreHeadingPage == true)
        {
            CurrentSectionBreadcrumb = "no H1 heading found yet";
        }
        else
        {
            CurrentSectionBreadcrumb = string.Empty;
        }

        CurrentPageBreadcrumb = CurrentPage?.Title ?? string.Empty;

        var imagePaths = new List<string>();
        var imageDir = GetCurrentImageDir();
        RenderedHtml = _htmlContentBuilder.Build(CurrentPage, IsOrgFile, imageDir, imagePaths);
        CurrentPageImagePaths = imagePaths;

        var isLastPage = CurrentPageIndex >= TotalPages - 1;
        NextButtonText = isLastPage ? "Done ^__^" : "Next →";
        NextButtonColor = isLastPage ? "#10B981" : "#2563EB";
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (CurrentPageIndex >= TotalPages - 1)
        {
            await CompleteDeck();
            return;
        }

        CurrentPageIndex++;
        UpdateCurrentPage();

        if (_currentDeck != null)
        {
            await _deckService.UpdateProgressAsync(_currentDeck.Id, CurrentPageIndex + 1);
            await _userService.RecordPageViewAsync();
        }
    }

    [RelayCommand]
    private async Task CompleteAndGoHome()
    {
        if (CurrentPageIndex >= TotalPages - 1)
        {
            await CompleteDeck();
            await _mainViewModel.ExitLearningAsync();
            return;
        }

        CurrentPageIndex++;
        UpdateCurrentPage();

        if (_currentDeck != null)
        {
            await _deckService.UpdateProgressAsync(_currentDeck.Id, CurrentPageIndex + 1);
            await _userService.RecordPageViewAsync();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            UpdateCurrentPage();
        }
    }

    private async Task CompleteDeck()
    {
        if (_currentDeck != null)
        {
            await _deckService.MarkCompletedAsync(_currentDeck.Id);
        }

        IsCompleted = true;
    }

    [RelayCommand]
    private async Task Exit()
    {
        await _mainViewModel.ExitLearningAsync();
    }

    [RelayCommand]
    private void GoToPage()
    {
        if (string.IsNullOrWhiteSpace(GoToPageInput))
        {
            GoToPageError = "Enter a page number.";
            return;
        }

        if (!int.TryParse(GoToPageInput.Trim(), out var page))
        {
            GoToPageError = "Invalid number.";
            return;
        }

        if (page < 1 || page > TotalPages)
        {
            GoToPageError = $"Enter a number between 1 and {TotalPages}.";
            return;
        }

        GoToPageError = null;
        CurrentPageIndex = page - 1;
        UpdateCurrentPage();
        IsGoToPageOpen = false;
    }

    [RelayCommand]
    private void CancelGoToPage()
    {
        GoToPageError = null;
        GoToPageInput = string.Empty;
        IsGoToPageOpen = false;
    }

    private string? GetCurrentImageDir()
    {
        if (_currentDeck == null || string.IsNullOrEmpty(_currentDeck.FileName))
        {
            return null;
        }

        return _decksPath;
    }
}
