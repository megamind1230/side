using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn;
using NextLearn.Data;
using NextLearn.Models;
using NextLearn.Services;

namespace NextLearn.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly DeckService _deckService;
    private readonly MainWindowViewModel _mainViewModel;
    private Deck? _deck;

    [ObservableProperty]
    private string _deckTitle = "";

    [ObservableProperty]
    private string _markdownContent = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _hasChanges;

    public EditorViewModel(DeckService deckService, MainWindowViewModel mainViewModel)
    {
        _deckService = deckService;
        _mainViewModel = mainViewModel;
    }

    public void LoadDeck(Deck deck)
    {
        _deck = deck;
        DeckTitle = deck.Title;
        LoadOrCreateMarkdown(deck);
    }

    private void LoadOrCreateMarkdown(Deck deck)
    {
        var decksFolder = Constants.DecksDir;
        Directory.CreateDirectory(decksFolder);

        var mdPath = Path.Combine(decksFolder, $"{deck.Id}.md");

        if (File.Exists(mdPath))
        {
            MarkdownContent = File.ReadAllText(mdPath);
            StatusMessage = "Loaded from file";
        }
        else
        {
            MarkdownContent = ConvertDeckToMarkdown(deck);
            File.WriteAllText(mdPath, MarkdownContent);
            StatusMessage = "Created new file";
        }
        HasChanges = false;
    }

    private string ConvertDeckToMarkdown(Deck deck)
    {
        var md = $"# {deck.Title}\n\n";
        md += $"{deck.Description}\n\n";
        md += $"---\n\n";

        foreach (var page in deck.Pages.OrderBy(p => p.PageNumber))
        {
            md += $"## {page.Title}\n\n";
            md += $"{page.TextContent}\n\n";
        }

        return md;
    }

    partial void OnMarkdownContentChanged(string value)
    {
        HasChanges = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (_deck == null) return;

        var decksFolder = Constants.DecksDir;
        Directory.CreateDirectory(decksFolder);

        var mdPath = Path.Combine(decksFolder, $"{_deck.Id}.md");
        File.WriteAllText(mdPath, MarkdownContent);

        HasChanges = false;
        StatusMessage = "Saved!";
    }

    [RelayCommand]
    private void ImportToDeck()
    {
        if (_deck == null) return;

        Save();

        var decksFolder = Constants.DecksDir;
        var mdPath = Path.Combine(decksFolder, $"{_deck.Id}.md");

        try
        {
            var content = File.ReadAllText(mdPath);
            var pages = ParseMarkdownToPages(content);

            using var context = new AppDbContext();
            var deckService = new DeckService(context, new UserService(context));

            var existingPages = context.Pages.Where(p => p.DeckId == _deck.Id).ToList();
            context.Pages.RemoveRange(existingPages);

            int pageNum = 1;
            foreach (var page in pages)
            {
                page.DeckId = _deck.Id;
                page.PageNumber = pageNum++;
                context.Pages.Add(page);
            }

            _deck.PageCount = pages.Count;
            context.SaveChanges();

            StatusMessage = $"Imported {pages.Count} pages!";
            _mainViewModel.LearningViewModel.StartLearning(_deck.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private System.Collections.Generic.List<Page> ParseMarkdownToPages(string md)
    {
        var pages = new System.Collections.Generic.List<Page>();
        var lines = md.Split('\n');
        var currentTitle = "";
        var currentContent = new System.Text.StringBuilder();
        bool inPage = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (inPage && currentTitle.Length > 0)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        Title = currentTitle,
                        TextContent = currentContent.ToString().Trim(),
                        ContentType = ContentType.Text
                    });
                }
                currentTitle = line.Substring(3).Trim();
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
                ContentType = ContentType.Text
            });
        }

        return pages;
    }

    [RelayCommand]
    private void Close()
    {
        _mainViewModel.CloseEditor();
    }
}
