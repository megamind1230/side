using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;

namespace NextLearn.Desktop.ViewModels;

public partial class FlashcardListViewModel : ViewModelBase
{
    private readonly FlashcardService _flashcardService;

    [ObservableProperty]
    private ObservableCollection<Flashcard> _flashcards = new();

    [ObservableProperty]
    private Flashcard? _currentFlashcard;

    [ObservableProperty]
    private bool _isReviewMode;

    [ObservableProperty]
    private bool _showAnswer;

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private string _reviewProgress = "0 / 0";

    public FlashcardListViewModel(FlashcardService flashcardService)
    {
        _flashcardService = flashcardService;
        LoadFlashcards();
    }

    public void Refresh()
    {
        LoadFlashcards();
    }

    private void LoadFlashcards()
    {
        var cards = _flashcardService.GetUserFlashcards();
        
        Flashcards.Clear();
        foreach (var card in cards)
        {
            Flashcards.Add(card);
        }

        ReviewProgress = $"{Flashcards.Count} cards";
    }

    [RelayCommand]
    private void StartReview()
    {
        if (Flashcards.Count == 0) return;

        IsReviewMode = true;
        CurrentIndex = 0;
        ShowAnswer = false;
        CurrentFlashcard = Flashcards[0];
        UpdateReviewProgress();
    }

    [RelayCommand]
    private void ShowCardAnswer()
    {
        ShowAnswer = true;
    }

    [RelayCommand]
    private async Task MarkKnown()
    {
        if (CurrentFlashcard != null)
        {
            await _flashcardService.MarkReviewedAsync(CurrentFlashcard.Id);
        }
        NextCard();
    }

    [RelayCommand]
    private void NextCard()
    {
        ShowAnswer = false;
        
        if (CurrentIndex < Flashcards.Count - 1)
        {
            CurrentIndex++;
            CurrentFlashcard = Flashcards[CurrentIndex];
        }
        else
        {
            IsReviewMode = false;
        }
        
        UpdateReviewProgress();
    }

    [RelayCommand]
    private async Task DeleteFlashcard(Flashcard? flashcard)
    {
        if (flashcard != null)
        {
            await _flashcardService.DeleteAsync(flashcard.Id);
            Flashcards.Remove(flashcard);
            UpdateReviewProgress();
        }
    }

    [RelayCommand]
    private void ExitReview()
    {
        IsReviewMode = false;
    }

    private void UpdateReviewProgress()
    {
        ReviewProgress = Flashcards.Count > 0 
            ? $"{CurrentIndex + 1} / {Flashcards.Count}" 
            : "0 / 0";
    }
}
