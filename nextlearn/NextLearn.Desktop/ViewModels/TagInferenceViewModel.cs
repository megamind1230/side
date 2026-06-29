using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;

#pragma warning disable CA2007 // ConfigureAwait — UI thread dispatch
#pragma warning disable CA1031 // catch specific — we want to catch all

namespace NextLearn.Desktop.ViewModels;

public partial class TagInferenceViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ITagInferenceService _tagInferenceService;
    private readonly IDeckFileWriter _deckFileWriter;
    private readonly string _decksPath;
    private List<Deck> _allDecks = [];

    [ObservableProperty]
    private ObservableCollection<Deck> _decks = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _inferenceStatus;

    [ObservableProperty]
    private double _inferenceProgress;

    [ObservableProperty]
    private bool _isInferring;

    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private string _existingTagsDisplay = string.Empty;

    [ObservableProperty]
    private string _proposedTagsDisplay = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    public bool HasDecks => Decks.Count > 0;

    public bool ShowDeckList => !IsInferring && !IsPreviewVisible && !HasError;

    partial void OnIsInferringChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnIsPreviewVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnDecksChanged(ObservableCollection<Deck> value) => OnPropertyChanged(nameof(HasDecks));

    private Deck? _currentDeck;
    private List<string> _suggestedTags = [];

    public TagInferenceViewModel(
        ISettingsService settingsService,
        ITagInferenceService tagInferenceService,
        IDeckFileWriter deckFileWriter,
        string? decksPath = null)
    {
        _settingsService = settingsService;
        _tagInferenceService = tagInferenceService;
        _deckFileWriter = deckFileWriter;
        _decksPath = Constants.GetDecksPath(decksPath);
    }

    public void LoadDecks()
    {
        if (!Directory.Exists(_decksPath))
        {
            Directory.CreateDirectory(_decksPath);
            return;
        }

        _allDecks = [];
        var extensions = new[] { "*.md", "*.org" };
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(_decksPath, ext, SearchOption.AllDirectories))
            {
                var deck = DeckFileParser.LoadDeckFromFile(file, _decksPath);
                if (deck != null)
                {
                    _allDecks.Add(deck);
                }
            }
        }

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allDecks
            : _allDecks.Where(d =>
                d.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (d.Tags ?? string.Empty).Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Decks = new ObservableCollection<Deck>(filtered.OrderBy(d => d.FileName));
    }

    [RelayCommand]
    private async Task InferTags(Deck? deck)
    {
        if (deck == null)
        {
            return;
        }

        var apiKey = _settingsService.GeminiApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "Configure your Gemini API key in Settings first.";
            HasError = true;
            return;
        }

        var filePath = Path.Combine(_decksPath, deck.FileName);
        if (!File.Exists(filePath))
        {
            ErrorMessage = "Deck file not found.";
            HasError = true;
            return;
        }

        _currentDeck = deck;
        _suggestedTags = [];
        IsPreviewVisible = false;
        IsInferring = true;
        HasError = false;
        ErrorMessage = null;

        // Ensure frontmatter is healthy before inferencing
        InferenceStatus = "Checking frontmatter…";
        _deckFileWriter.EnsureHealthyFrontmatter(filePath, out _);

        // Reload deck so tags/desc/title reflect any changes
        var reloaded = DeckFileParser.LoadDeckFromFile(filePath, _decksPath);
        if (reloaded != null)
        {
            _currentDeck = reloaded;
        }

        try
        {
            InferenceStatus = "Parsing deck content…";
            InferenceProgress = 10;
            await Task.Delay(100);

            var textContent = string.Join("\n", deck.Pages.Select(p => p.TextContent));
            var existingTags = deck.Tags ?? string.Empty;

            InferenceStatus = "Contacting Gemini…";
            InferenceProgress = 30;

            var result = await _tagInferenceService.InferTagsAsync(textContent, existingTags, apiKey);

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                HasError = true;
                IsInferring = false;
                InferenceStatus = null;
                return;
            }

            _suggestedTags = result.SuggestedTags;

            InferenceStatus = "Formatting results…";
            InferenceProgress = 90;
            await Task.Delay(100);

            ExistingTagsDisplay = string.IsNullOrWhiteSpace(existingTags) ? "(none)" : existingTags;
            ProposedTagsDisplay = string.Join(", ", _suggestedTags);

            InferenceStatus = "Ready";
            InferenceProgress = 100;
            IsPreviewVisible = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsInferring = false;
        }
    }

    [RelayCommand]
    private async Task ApplyTags()
    {
        if (_currentDeck == null || _suggestedTags.Count == 0)
        {
            return;
        }

        var filePath = Path.Combine(_decksPath, _currentDeck.FileName);
        if (!File.Exists(filePath))
        {
            ErrorMessage = "Deck file not found. It may have been moved or deleted.";
            HasError = true;
            return;
        }

        if (_deckFileWriter.AppendTags(filePath, _suggestedTags, out var error))
        {
            var reloaded = DeckFileParser.LoadDeckFromFile(filePath, _decksPath);
            if (reloaded != null)
            {
                var idx = _allDecks.FindIndex(d => d.Id == _currentDeck.Id);
                if (idx >= 0)
                {
                    _allDecks[idx] = reloaded;
                }

                var idx2 = Decks.IndexOf(_currentDeck);
                if (idx2 >= 0)
                {
                    Decks[idx2] = reloaded;
                }
            }

            CancelPreview();
        }
        else
        {
            ErrorMessage = error ?? "Failed to write tags to file.";
            HasError = true;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelPreview()
    {
        IsPreviewVisible = false;
        InferenceStatus = null;
        InferenceProgress = 0;
        _currentDeck = null;
        _suggestedTags = [];
        ExistingTagsDisplay = string.Empty;
        ProposedTagsDisplay = string.Empty;
        HasError = false;
        ErrorMessage = null;
    }
}
