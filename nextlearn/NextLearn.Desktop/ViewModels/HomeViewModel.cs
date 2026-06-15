using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    private List<Deck> _allDecks = new();

    [ObservableProperty]
    private ObservableCollection<Deck> _decks = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _useRegex;

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
        _allDecks.Clear();
        LoadDecks();
    }

    private void LoadDecks()
    {
        var decksPath = _decksPath;
        if (!Directory.Exists(decksPath))
        {
            Directory.CreateDirectory(decksPath);
            return;
        }

        if (_allDecks.Count == 0)
        {
            var extensions = new[] { "*.md", "*.org" };
            foreach (var ext in extensions)
            {
                foreach (var file in Directory.GetFiles(decksPath, ext))
                {
                    var deck = DeckFileParser.LoadDeckFromFile(file);
                    if (deck != null)
                    {
                        _deckService.SaveOrUpdateDeck(deck);
                        _allDecks.Add(deck);
                    }
                }
            }
        }

        Decks.Clear();
        foreach (var deck in _allDecks)
            Decks.Add(deck);

        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        var tokens = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var toRemove = new List<Deck>();

        foreach (var deck in _allDecks)
        {
            var deckTags = (deck.Tags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .ToHashSet();

            var matched = true;

            foreach (var token in tokens)
            {
                if (token.StartsWith('#'))
                {
                    var tag = token[1..].ToLowerInvariant();
                    bool tagMatched;

                    if (UseRegex)
                    {
                        try
                        {
                            tagMatched = deckTags.Any(t => Regex.IsMatch(t, tag, RegexOptions.IgnoreCase));
                        }
                        catch (ArgumentException)
                        {
                            tagMatched = false;
                        }
                    }
                    else
                    {
                        tagMatched = deckTags.Any(t => t.StartsWith(tag));
                    }

                    if (!tagMatched)
                    {
                        matched = false;
                        break;
                    }
                }
                else
                {
                    if (!TokenMatches(deck, token))
                    {
                        matched = false;
                        break;
                    }
                }
            }

            if (!matched)
                toRemove.Add(deck);
        }

        foreach (var deck in toRemove)
            Decks.Remove(deck);
    }

    private bool TokenMatches(Deck deck, string token)
    {
        if (UseRegex)
        {
            try
            {
                return Regex.IsMatch(deck.Title, token, RegexOptions.IgnoreCase) ||
                       Regex.IsMatch(deck.Description, token, RegexOptions.IgnoreCase) ||
                       Regex.IsMatch(deck.FileName, token, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return deck.Title.Contains(token, StringComparison.OrdinalIgnoreCase) ||
               deck.Description.Contains(token, StringComparison.OrdinalIgnoreCase) ||
               deck.FileName.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchTextChanged(string value)
    {
        LoadDecks();
    }

    partial void OnUseRegexChanged(bool value)
    {
        LoadDecks();
    }

    [RelayCommand]
    private void LearnDeck(Deck deck)
    {
        _mainViewModel.NavigateToLearningByDeck(deck);
    }

    [RelayCommand]
    private void PinDeck(Deck deck)
    {
        _deckService.PinDeck(deck.Id, _decksPath);
        Refresh();
    }

    [RelayCommand]
    private void UnpinDeck(Deck deck)
    {
        _deckService.UnpinDeck(deck.Id, _decksPath);
        Refresh();
    }

    [RelayCommand]
    private void ArchiveDeck(Deck deck)
    {
        _deckService.ArchiveDeck(deck.Id, _decksPath);
        Refresh();
    }

    [RelayCommand]
    private void TogglePinDeck(Deck deck)
    {
        if (deck.IsPinned)
            _deckService.UnpinDeck(deck.Id, _decksPath);
        else
            _deckService.PinDeck(deck.Id, _decksPath);
        Refresh();
    }

    public void FocusSearch()
    {
        SearchText = "";
    }
}
