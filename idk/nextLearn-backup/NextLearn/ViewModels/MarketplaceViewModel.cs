using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Data;
using NextLearn.Models;
using NextLearn.Services;

namespace NextLearn.ViewModels;

public partial class MarketplaceViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _marketplaceUrl = "https://nextlearn.app/marketplace";

    public MarketplaceViewModel()
    {
    }
}

public partial class DownloadedDecksViewModel : ViewModelBase
{
    private readonly DeckService _deckService;
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private ObservableCollection<Deck> _decks = new();

    public DownloadedDecksViewModel(DeckService deckService, MainWindowViewModel mainViewModel)
    {
        _deckService = deckService;
        _mainViewModel = mainViewModel;
        LoadDecks();
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

        using var context = new AppDbContext();
        
        foreach (var mdFile in Directory.GetFiles(decksPath, "*.md"))
        {
            var deckId = Path.GetFileNameWithoutExtension(mdFile);
            if (Guid.TryParse(deckId, out var guid))
            {
                var deck = context.Decks.FirstOrDefault(d => d.Id == guid);
                if (deck != null)
                {
                    Decks.Add(deck);
                }
            }
        }
    }

    [RelayCommand]
    private void LearnDeck(Deck deck)
    {
        _mainViewModel.NavigateToLearning(deck.Id);
    }
}
