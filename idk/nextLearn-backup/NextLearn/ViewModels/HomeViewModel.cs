using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn;
using NextLearn.Data;
using NextLearn.Models;
using NextLearn.Services;

namespace NextLearn.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly DeckService _deckService;
    private readonly MainWindowViewModel _mainViewModel;
    private FileSystemWatcher? _watcher;

    [ObservableProperty]
    private ObservableCollection<Deck> _decks = new();

    [ObservableProperty]
    private string _selectedCategory = "all";

    [ObservableProperty]
    private string _searchText = "";

    public string[] Categories => new[] { "all", "children", "students", "moms", "general" };

    public HomeViewModel(DeckService deckService, MainWindowViewModel mainViewModel)
    {
        _deckService = deckService;
        _mainViewModel = mainViewModel;
        SetupFileWatcher();
        LoadDecks();
    }

    private void SetupFileWatcher()
    {
        var decksPath = Constants.DecksDir;
        if (!Directory.Exists(decksPath))
        {
            Directory.CreateDirectory(decksPath);
        }

        _watcher = new FileSystemWatcher(decksPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += (s, e) => Refresh();
        _watcher.Changed += (s, e) => Refresh();
        _watcher.Deleted += (s, e) => Refresh();
        _watcher.Renamed += (s, e) => Refresh();
    }

    public void Refresh()
    {
        LoadDecks();
    }

    private void LoadDecks()
    {
        Decks.Clear();

        var decksPath = Constants.DecksDir;
        if (!Directory.Exists(decksPath))
        {
            Directory.CreateDirectory(decksPath);
            return;
        }

        var extensions = new[] { "*.md", "*.org" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(decksPath, ext))
            {
                var deck = LoadDeckFromFile(file);
                if (deck != null)
                {
                    AddDeckIfMatches(deck);
                }
            }
        }
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

    private void AddDeckIfMatches(Deck deck)
    {
        if (SelectedCategory != "all" && deck.Category != SelectedCategory)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (!deck.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !deck.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        Decks.Add(deck);
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
        LoadDecks();
    }

    [RelayCommand]
    private void LearnDeck(Deck deck)
    {
        _mainViewModel.NavigateToLearningByDeck(deck);
    }

    partial void OnSearchTextChanged(string value)
    {
        LoadDecks();
    }

    public void FocusSearch() { }
}
