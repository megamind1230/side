using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Data;
using NextLearn.Models;
using NextLearn.Services;

namespace NextLearn.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly UserService _userService;
    private readonly DeckService _deckService;
    private readonly FlashcardService _flashcardService;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _title = "NextLearn";

    [ObservableProperty]
    private bool _isLearning;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private Deck? _editingDeck;

    [ObservableProperty]
    private bool _isHomeSearchOpen;

    [ObservableProperty]
    private bool _isMarketplace;

    public HomeViewModel HomeViewModel { get; }
    public LearningViewModel LearningViewModel { get; }
    public FlashcardListViewModel FlashcardListViewModel { get; }
    public EditorViewModel EditorViewModel { get; }
    public MarketplaceViewModel MarketplaceViewModel { get; }

    public MainWindowViewModel()
    {
        _context = new AppDbContext();
        _context.Database.EnsureCreated();
        
        _userService = new UserService(_context);
        _deckService = new DeckService(_context, _userService);
        _flashcardService = new FlashcardService(_context, _userService);

        HomeViewModel = new HomeViewModel(_deckService, this);
        LearningViewModel = new LearningViewModel(_deckService, _flashcardService, _userService, this);
        FlashcardListViewModel = new FlashcardListViewModel(_flashcardService);
        EditorViewModel = new EditorViewModel(_deckService, this);
        MarketplaceViewModel = new MarketplaceViewModel();

        CurrentView = HomeViewModel;
    }

    [RelayCommand]
    public void OpenEditor()
    {
        if (LearningViewModel.CurrentPage == null) return;
        
        var deckId = LearningViewModel.CurrentPage.DeckId;
        var deck = _deckService.GetDeckById(deckId);
        if (deck == null) return;

        EditingDeck = deck;
        EditorViewModel.LoadDeck(deck);
        IsEditorOpen = true;
    }

    public void CloseEditor()
    {
        IsEditorOpen = false;
        EditingDeck = null;
    }

    [RelayCommand]
    public void NavigateToHome()
    {
        IsLearning = false;
        IsMarketplace = false;
        CurrentView = HomeViewModel;
        HomeViewModel.Refresh();
    }

    [RelayCommand]
    public void NavigateToLearning(Guid deckId)
    {
        IsLearning = true;
        var deck = HomeViewModel.Decks.FirstOrDefault(d => d.Id == deckId);
        if (deck != null)
        {
            LearningViewModel.SetCurrentDeck(deck);
        }
        else
        {
            LearningViewModel.StartLearning(deckId);
        }
        CurrentView = LearningViewModel;
    }

    public void NavigateToLearningByDeck(Deck deck)
    {
        IsLearning = true;
        LearningViewModel.SetCurrentDeck(deck);
        CurrentView = LearningViewModel;
    }

    [RelayCommand]
    public void NavigateToFlashcards()
    {
        IsLearning = false;
        FlashcardListViewModel.Refresh();
        CurrentView = FlashcardListViewModel;
    }

    [RelayCommand]
    public void NavigateToMarketplace()
    {
        IsLearning = false;
        IsMarketplace = true;
        CurrentView = MarketplaceViewModel;
    }

    [RelayCommand]
    public void ExitLearning()
    {
        IsLearning = false;
        NavigateToHome();
    }

    [RelayCommand]
    public void ShowHomeSearch()
    {
        IsHomeSearchOpen = true;
    }

    [RelayCommand]
    public void CloseHomeSearch()
    {
        IsHomeSearchOpen = false;
    }
}
