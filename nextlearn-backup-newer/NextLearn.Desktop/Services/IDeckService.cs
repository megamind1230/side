using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public interface IDeckService
{
    List<Deck> GetPublishedDecks();

    Deck? GetDeckById(Guid deckId);

    void SaveOrUpdateDeck(Deck deck);

    Page? GetPage(Guid pageId);

    Task<UserProgress?> GetUserProgressAsync(Guid deckId);

    Task<UserProgress> StartLearningAsync(Guid deckId);

    Task UpdateProgressAsync(Guid deckId, int currentPage);

    Task MarkCompletedAsync(Guid deckId);

    List<ActiveLearning> GetActiveLearningSlots();
}
