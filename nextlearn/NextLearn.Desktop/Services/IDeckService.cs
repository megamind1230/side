using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

/// <summary>Manages deck persistence and learning progress in the database.</summary>
public interface IDeckService
{
    /// <summary>Returns published and reviewed decks ordered by creation date descending.</summary>
    /// <returns>List of published decks.</returns>
    List<Deck> GetPublishedDecks();

    /// <summary>Finds a deck by its ID, including pages ordered by page number.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <returns>The deck with pages, or null if not found.</returns>
    Deck? GetDeckById(Guid deckId);

    /// <summary>Creates a new deck or updates an existing one (includes pages).</summary>
    /// <param name="deck">The deck to save.</param>
    void SaveOrUpdateDeck(Deck deck);

    /// <summary>Updates deck metadata (FileName, IsPinned, IsArchived) without touching pages.</summary>
    /// <param name="deck">The deck with updated metadata.</param>
    void SyncDeckMetadata(Deck deck);

    /// <summary>Returns a single page by ID.</summary>
    /// <param name="pageId">The page ID.</param>
    /// <returns>The page, or null if not found.</returns>
    Page? GetPage(Guid pageId);

    /// <summary>Gets the current user's progress for a given deck, or null if none exists.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <returns>The user progress, or null.</returns>
    Task<UserProgress?> GetUserProgressAsync(Guid deckId);

    /// <summary>Starts learning a deck, creating a progress row and an active slot if needed.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <returns>The user progress record.</returns>
    Task<UserProgress> StartLearningAsync(Guid deckId);

    /// <summary>Updates the current page for a deck's learning progress.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <param name="currentPage">The new current page number.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateProgressAsync(Guid deckId, int currentPage);

    /// <summary>Marks a deck as completed for the current user.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkCompletedAsync(Guid deckId);

    /// <summary>Returns active learning slots for the current user, ordered by slot number.</summary>
    /// <returns>The active learning slots.</returns>
    List<ActiveLearning> GetActiveLearningSlots();
}