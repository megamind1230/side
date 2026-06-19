using System;

namespace NextLearn.Desktop.Services;

/// <summary>Manages deck file pinning and archiving via file rename operations.</summary>
public interface IDeckFileService
{
    /// <summary>Archives a deck by appending ~ to its filename.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <param name="decksPath">The decks directory path.</param>
    void ArchiveDeck(Guid deckId, string decksPath);

    /// <summary>Restores an archived deck by removing the ~ suffix.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <param name="decksPath">The decks directory path.</param>
    void UnarchiveDeck(Guid deckId, string decksPath);

    /// <summary>Pins a deck by prepending + to its filename.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <param name="decksPath">The decks directory path.</param>
    void PinDeck(Guid deckId, string decksPath);

    /// <summary>Unpins a deck by removing the + prefix.</summary>
    /// <param name="deckId">The deck ID.</param>
    /// <param name="decksPath">The decks directory path.</param>
    void UnpinDeck(Guid deckId, string decksPath);
}