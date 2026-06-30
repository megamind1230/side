using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Serilog;

#pragma warning disable CA2007 // ConfigureAwait — UI thread dispatch
#pragma warning disable CA1031 // catch specific — we want to catch all

namespace NextLearn.Desktop.ViewModels;

public partial class FlashcardViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IFlashcardService _flashcardService;
    private readonly string _decksPath;
    private List<Deck> _allDecks = [];
    private int _generationId;

    [ObservableProperty]
    private ObservableCollection<Deck> _decks = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private string? _generationStatus;

    [ObservableProperty]
    private double _generationProgress;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private string _previewHeader = string.Empty;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasSuccessMessage;

    private Deck? _currentDeck;
    private string _savedContent = string.Empty;
    private int _savedCount;
    private FlashcardGenerationMode? _currentMode;

    public bool HasDecks => Decks.Count > 0;

    public bool ShowDeckList => !IsGenerating && !IsPreviewVisible && !HasError && !HasSuccessMessage;

    partial void OnIsGeneratingChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnIsPreviewVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnHasSuccessMessageChanged(bool value) => OnPropertyChanged(nameof(ShowDeckList));

    partial void OnDecksChanged(ObservableCollection<Deck> value) => OnPropertyChanged(nameof(HasDecks));

    public FlashcardViewModel(
        ISettingsService settingsService,
        IFlashcardService flashcardService,
        string? decksPath = null)
    {
        _settingsService = settingsService;
        _flashcardService = flashcardService;
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

    partial void OnUseRegexChanged(bool value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Decks = new ObservableCollection<Deck>(_allDecks.OrderBy(d => d.FileName));
            return;
        }

        var tokens = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = _allDecks.Where(deck =>
        {
            var deckTags = (deck.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .ToHashSet();

            return tokens.All(token => TokenMatchesDeck(deck, deckTags, token));
        }).ToList();

        Decks = new ObservableCollection<Deck>(filtered.OrderBy(d => d.FileName));
    }

    private bool TokenMatchesDeck(Deck deck, HashSet<string> deckTags, string token)
    {
        if (token.StartsWith('#'))
        {
            var tag = token[1..].ToLowerInvariant();

            if (UseRegex)
            {
                try
                {
                    return deckTags.Any(t => Regex.IsMatch(t, tag, RegexOptions.IgnoreCase));
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return deckTags.Any(t => t.StartsWith(tag));
        }

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

    private async Task Generate(Deck? deck, FlashcardGenerationMode mode)
    {
        if (deck == null)
        {
            return;
        }

        var apiKey = _settingsService.GeminiApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Flashcard generation failed: no API key configured");
            ErrorMessage = "Configure your Gemini API key in Settings first.";
            HasError = true;
            return;
        }

        var filePath = Path.Combine(_decksPath, deck.FileName);
        if (!File.Exists(filePath))
        {
            Log.Error("Flashcard generation failed: deck file not found: {Path}", filePath);
            ErrorMessage = "Deck file not found.";
            HasError = true;
            return;
        }

        var myId = Interlocked.Increment(ref _generationId);
        _currentDeck = deck;
        _currentMode = null;
        _savedContent = string.Empty;
        PreviewHeader = string.Empty;
        PreviewText = string.Empty;
        IsPreviewVisible = false;
        IsGenerating = true;
        HasError = false;
        ErrorMessage = null;
        SuccessMessage = null;
        HasSuccessMessage = false;

        try
        {
            GenerationStatus = "Parsing deck content…";
            GenerationProgress = 10;
            await Task.Delay(100);

            var textContent = string.Join("\n", deck.Pages.Select(p => p.TextContent));

            if (string.IsNullOrWhiteSpace(textContent))
            {
                Log.Error("Flashcard generation failed: deck has no text content");
                ErrorMessage = "Deck has no content to generate flashcards from.";
                HasError = true;
                return;
            }

            GenerationStatus = "Contacting Gemini…";
            GenerationProgress = 30;

            var result = await _flashcardService.GenerateFlashcardsAsync(textContent, apiKey, mode);

            if (_generationId != myId)
            {
                return;
            }

            if (!result.Success)
            {
                Log.Error("Flashcard generation failed: {Error}", result.Error);
                ErrorMessage = result.Error;
                HasError = true;
                return;
            }

            _currentMode = result.Mode;
            _savedContent = result.Content;
            _savedCount = result.Count;

            GenerationStatus = "Formatting results…";
            GenerationProgress = 90;
            await Task.Delay(100);

            var label = mode == FlashcardGenerationMode.Basic ? "Basic" : "Cloze";
            PreviewHeader = $"{label} Flashcards ({result.Count} cards)";
            PreviewText = result.Content;

            GenerationStatus = "Ready";
            GenerationProgress = 100;
            IsPreviewVisible = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Flashcard generation unexpected error");
            ErrorMessage = $"Unexpected error: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private Task GenerateBasic(Deck? deck) => Generate(deck, FlashcardGenerationMode.Basic);

    [RelayCommand]
    private Task GenerateCloze(Deck? deck) => Generate(deck, FlashcardGenerationMode.Cloze);

    [RelayCommand]
    private async Task AcceptFlashcards()
    {
        if (_currentDeck == null || _currentMode == null || string.IsNullOrEmpty(_savedContent))
        {
            return;
        }

        var flashcardsPath = _settingsService.ResolvedFlashcardsPath;
        Directory.CreateDirectory(flashcardsPath);

        var baseName = Path.GetFileName(_currentDeck.FileName);
        var suffix = _currentMode == FlashcardGenerationMode.Basic ? ".basic.txt" : ".cloze.txt";
        var savePath = Path.Combine(flashcardsPath, baseName + suffix);

        try
        {
            await File.WriteAllTextAsync(savePath, _savedContent);

            var label = _currentMode == FlashcardGenerationMode.Basic ? "basic" : "cloze";
            SuccessMessage = $"✓ Saved {label} ({_savedCount} cards) to:\n  {savePath}";

            HasSuccessMessage = true;

            await Task.Delay(3000);
            ResetToDeckList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save flashcards");
            ErrorMessage = $"Failed to save flashcards: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private void CancelPreview()
    {
        ResetToDeckList();
    }

    private void ResetToDeckList()
    {
        IsPreviewVisible = false;
        SuccessMessage = null;
        HasSuccessMessage = false;
        GenerationStatus = null;
        GenerationProgress = 0;
        _currentDeck = null;
        _currentMode = null;
        _savedContent = string.Empty;
        _savedCount = 0;
        PreviewHeader = string.Empty;
        PreviewText = string.Empty;
        HasError = false;
        ErrorMessage = null;
    }
}
