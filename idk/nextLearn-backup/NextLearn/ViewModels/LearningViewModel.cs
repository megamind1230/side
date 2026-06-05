using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn;
using NextLearn.Models;
using NextLearn.Services;

namespace NextLearn.ViewModels;

public partial class LearningViewModel : ViewModelBase
{
    private readonly DeckService _deckService;
    private readonly FlashcardService _flashcardService;
    private readonly UserService _userService;
    private readonly MainWindowViewModel _mainViewModel;

    private Deck? _currentDeck;
    private List<Page> _pages = new();
    private UserProgress? _progress;

    [ObservableProperty]
    private int _currentPageIndex;

    [ObservableProperty]
    private Page? _currentPage;

    [ObservableProperty]
    private string _deckTitle = "";

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _progressText = "1 / 10";

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

    public LearningViewModel(DeckService deckService, FlashcardService flashcardService, 
        UserService userService, MainWindowViewModel mainViewModel)
    {
        _deckService = deckService;
        _flashcardService = flashcardService;
        _userService = userService;
        _mainViewModel = mainViewModel;
    }

    public void SetCurrentDeck(Deck deck)
    {
        _currentDeck = deck;
        _pages = deck.Pages.OrderBy(p => p.PageNumber).ToList();
        TotalPages = _pages.Count;
        DeckTitle = deck.Title;
        
        CurrentPageIndex = 0;
        UpdateCurrentPage();
    }

    public async void StartLearning(Guid deckId)
    {
        var decksPath = Constants.DecksDir;
        if (!Directory.Exists(decksPath))
        {
            Directory.CreateDirectory(decksPath);
            return;
        }

        var extensions = new[] { "*.md", "*.org" };
        Deck? deck = null;
        
        foreach (var ext in extensions)
        {
            var files = Directory.GetFiles(decksPath, ext);
            foreach (var file in files)
            {
                var loadedDeck = LoadDeckFromFile(file);
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
        DeckTitle = deck.Title;

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

    private Deck? LoadDeckFromFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            var pages = ParsePagesFromContent(content);
            if (pages.Count == 0) return null;

            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                Title = fileName,
                Description = ExtractDescription(content),
                Category = ExtractCategory(fileName),
                Difficulty = "lvl0",
                IsPublished = true,
                IsReviewed = true,
                CreatedAt = File.GetCreationTime(filePath),
                PageCount = pages.Count,
                Pages = pages
            };

            foreach (var page in pages)
            {
                page.DeckId = deck.Id;
            }

            return deck;
        }
        catch
        {
            return null;
        }
    }

    private string ExtractDescription(string content)
    {
        var lines = content.Split('\n');
        foreach (var line in lines.Skip(1))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#") && !trimmed.StartsWith("##"))
            {
                return trimmed.Length > 200 ? trimmed.Substring(0, 200) : trimmed;
            }
        }
        return "";
    }

    private string ExtractCategory(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("whatsapp") || lower.Contains("discord") || lower.Contains("social")) return "moms";
        if (lower.Contains("number") || lower.Contains("kids") || lower.Contains("children")) return "children";
        if (lower.Contains("code") || lower.Contains("emacs") || lower.Contains("vim") || lower.Contains("shortcut")) return "students";
        return "general";
    }

    private List<Page> ParsePagesFromContent(string content)
    {
        var pages = new List<Page>();
        var lines = content.Split('\n');
        
        var currentTitle = "";
        var currentContent = new System.Text.StringBuilder();
        bool inPage = false;
        int pageNum = 1;

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^#{1,2}\s+"))
            {
                if (inPage && currentTitle.Length > 0)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        Title = currentTitle,
                        TextContent = currentContent.ToString().Trim(),
                        ContentType = ContentType.Text,
                        PageNumber = pageNum++
                    });
                }
                currentTitle = Regex.Replace(line, @"^#{1,2}\s+", "").Trim();
                currentContent.Clear();
                inPage = true;
            }
            else if (inPage)
            {
                currentContent.AppendLine(line);
            }
        }

        if (inPage && currentTitle.Length > 0)
        {
            pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                Title = currentTitle,
                TextContent = currentContent.ToString().Trim(),
                ContentType = ContentType.Text,
                PageNumber = pageNum++
            });
        }

        if (pages.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                Title = "Content",
                TextContent = content.Trim(),
                ContentType = ContentType.Text,
                PageNumber = 1
            });
        }

        return pages;
    }

    private void UpdateCurrentPage()
    {
        if (_pages.Count == 0) return;

        CurrentPage = _pages[CurrentPageIndex];
        ProgressText = $"{CurrentPageIndex + 1} / {TotalPages}";
        CanGoBack = CurrentPageIndex > 0;
        CanGoNext = CurrentPageIndex < TotalPages - 1;
        IsCompleted = false;
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
    private void Exit()
    {
        _mainViewModel.ExitLearning();
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

    public string GetDeckMarkdownPath(Guid deckId)
    {
        if (_currentDeck == null) return "";

        var decksFolder = Constants.DecksDir;
        Directory.CreateDirectory(decksFolder);

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
