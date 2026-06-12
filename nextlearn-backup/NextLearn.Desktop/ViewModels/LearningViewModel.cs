using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;

namespace NextLearn.Desktop.ViewModels;

public partial class LearningViewModel : ViewModelBase
{
    private readonly DeckService _deckService;
    private readonly FlashcardService _flashcardService;
    private readonly UserService _userService;
    private readonly MainWindowViewModel _mainViewModel;
    private readonly string _decksPath;

    private Deck? _currentDeck;
    private List<Page> _pages = new();
    private UserProgress? _progress;

    [ObservableProperty]
    private int _currentPageIndex;

    [ObservableProperty]
    private Page? _currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSection))]
    private string _currentSectionTitle = "";

    [ObservableProperty]
    private string _currentSectionDisplay = "";

    [ObservableProperty]
    private string _currentSectionBreadcrumb = "";

    [ObservableProperty]
    private string _currentPageBreadcrumb = "";

    [ObservableProperty]
    private string _renderedHtml = "";

    [ObservableProperty]
    private bool _isOrgFile;

    public bool HasSection => !string.IsNullOrEmpty(CurrentSectionTitle);

    [ObservableProperty]
    private string _deckTitle = "";

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
    private bool _showFeedbackDialog;

    [ObservableProperty]
    private string _feedbackMessage = "";

    [ObservableProperty]
    private string _feedbackTopic = "";

    [ObservableProperty]
    private bool _showSearch;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private List<Page> _searchResults = new();

    [ObservableProperty]
    private List<string> _currentPageImagePaths = new();

    public LearningViewModel(DeckService deckService, FlashcardService flashcardService, 
        UserService userService, MainWindowViewModel mainViewModel, string? decksPath = null)
    {
        _deckService = deckService;
        _flashcardService = flashcardService;
        _userService = userService;
        _mainViewModel = mainViewModel;
        _decksPath = Constants.GetDecksPath(decksPath);
    }

    public async Task SetCurrentDeck(Deck deck)
    {
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
            if (CurrentPageIndex >= _pages.Count) CurrentPageIndex = _pages.Count - 1;
        }
        else
        {
            CurrentPageIndex = 0;
        }
        UpdateCurrentPage();
    }

    public async Task SaveProgressAsync()
    {
        if (_currentDeck != null)
        {
            await _deckService.UpdateProgressAsync(_currentDeck.Id, CurrentPageIndex + 1);
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
            if (deck != null) break;
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
        if (CurrentPageIndex < 0) CurrentPageIndex = 0;
        if (CurrentPageIndex >= _pages.Count) CurrentPageIndex = _pages.Count - 1;

        UpdateCurrentPage();
    }

    private void UpdateCurrentPage()
    {
        if (_pages.Count == 0) return;

        CurrentPage = _pages[CurrentPageIndex];
        CurrentSectionTitle = CurrentPage?.SectionTitle ?? "";
        CurrentSectionDisplay = !string.IsNullOrEmpty(CurrentSectionTitle) ? $" → {CurrentSectionTitle}" : "";
        ProgressText = $"{CurrentPageIndex + 1} / {TotalPages}";
        CanGoBack = CurrentPageIndex > 0;
        CanGoNext = CurrentPageIndex < TotalPages - 1;
        IsCompleted = false;

        CurrentSectionBreadcrumb = CurrentSectionTitle ?? "";
        CurrentPageBreadcrumb = CurrentPage?.Title ?? "";

        var imagePaths = new List<string>();
        var imageDir = GetCurrentImageDir();
        RenderedHtml = HtmlContentBuilder.Build(CurrentPage, IsOrgFile, imageDir, imagePaths);
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
            await _mainViewModel.ExitLearning();
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
    private async Task AddToFlashcards()
    {
        if (CurrentPage != null)
        {
            await _flashcardService.GenerateFromPageAsync(CurrentPage.Id);
        }
    }

    [RelayCommand]
    private void OpenFeedback()
    {
        ShowFeedbackDialog = true;
    }

    [RelayCommand]
    private async Task SubmitFeedback()
    {
        if (_currentDeck != null && !string.IsNullOrWhiteSpace(FeedbackTopic))
        {
            await _deckService.GiveFeedbackAsync(
                _currentDeck.Id, 
                CurrentPage?.Id, 
                FeedbackMessage, 
                FeedbackTopic
            );
        }
        ShowFeedbackDialog = false;
        FeedbackMessage = "";
        FeedbackTopic = "";
    }

    [RelayCommand]
    private void CancelFeedback()
    {
        ShowFeedbackDialog = false;
        FeedbackMessage = "";
        FeedbackTopic = "";
    }

    [RelayCommand]
    private async Task Exit()
    {
        await _mainViewModel.ExitLearning();
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        ShowSearch = !ShowSearch;
        if (ShowSearch)
        {
            SearchText = "";
            SearchResults = _pages;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults = _pages;
        }
        else
        {
            SearchResults = _pages
                .Where(p => p.Title.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                           (p.TextContent?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }
    }

    private string? GetCurrentImageDir()
    {
        if (_currentDeck == null || string.IsNullOrEmpty(_currentDeck.FileName))
            return null;
        return _decksPath;
    }

    public string GetDeckMarkdownPath(Guid deckId)
    {
        if (_currentDeck == null) return "";

        var decksFolder = _decksPath;

        var slug = ToSlug(_currentDeck.Title);
        
        var mdPath = Path.Combine(decksFolder, $"{slug}.md");
        var orgPath = Path.Combine(decksFolder, $"{slug}.org");

        string? existingPath = null;
        if (File.Exists(mdPath)) existingPath = mdPath;
        else if (File.Exists(orgPath)) existingPath = orgPath;

        if (existingPath != null)
        {
            return existingPath;
        }

        if (_currentDeck != null)
        {
            var md = $"# {_currentDeck.Title}\n\n{_currentDeck.Description}\n\n---\n\n";
            foreach (var page in _pages)
            {
                md += $"## {page.Title}\n\n{page.TextContent}\n\n";
            }
            File.WriteAllText(mdPath, md);
        }

        return mdPath;
    }

    private string ToSlug(string title)
    {
        return title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace("'", "")
            .Replace("\"", "");
    }
}
