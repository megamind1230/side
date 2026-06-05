using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;

namespace NextLearn.Desktop.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly DeckService _deckService;
    private readonly MainWindowViewModel _mainViewModel;
    private readonly string _decksPath;
    private FileSystemWatcher? _watcher;

    [ObservableProperty]
    private ObservableCollection<Deck> _decks = new();

    [ObservableProperty]
    private string _searchText = "";

    public HomeViewModel(DeckService deckService, MainWindowViewModel mainViewModel, string? decksPath = null)
    {
        _deckService = deckService;
        _mainViewModel = mainViewModel;
        _decksPath = Constants.GetDecksPath(decksPath);
        SetupFileWatcher();
        LoadDecks();
    }

    private void SetupFileWatcher()
    {
        var decksPath = _decksPath;
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

        var decksPath = _decksPath;
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
                var deck = DeckFileParser.LoadDeckFromFile(file);
                if (deck != null)
                {
                    _deckService.SaveOrUpdateDeck(deck);
                    AddDeckIfMatches(deck);
                }
            }
        }
    }

    private void AddDeckIfMatches(Deck deck)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Decks.Add(deck);
            return;
        }

        var tokens = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var deckTags = (deck.Tags ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        foreach (var token in tokens)
        {
            if (token.StartsWith('#'))
            {
                    var tag = token[1..].ToLowerInvariant();
                    if (!deckTags.Any(t => t.StartsWith(tag)))
                        return;
            }
            else
            {
                if (!deck.Title.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                    !deck.Description.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                    !deck.FileName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        Decks.Add(deck);
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

    public void FocusSearch()
    {
        SearchText = "";
    }
}
