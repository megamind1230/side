using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

/// <summary>Manages deck file pinning and archiving via file rename operations.</summary>
public interface IDeckFileService
{
    /// <summary>Archives a deck by appending ~ to its filename.</summary>
    /// <param name="deck">The deck to archive (FileName is updated in-place).</param>
    /// <param name="decksPath">The decks directory path.</param>
    void ArchiveDeck(Deck deck, string decksPath);

    /// <summary>Restores an archived deck by removing the ~ suffix.</summary>
    /// <param name="deck">The deck to restore (FileName is updated in-place).</param>
    /// <param name="decksPath">The decks directory path.</param>
    void UnarchiveDeck(Deck deck, string decksPath);

    /// <summary>Pins a deck by prepending + to its filename.</summary>
    /// <param name="deck">The deck to pin (FileName is updated in-place).</param>
    /// <param name="decksPath">The decks directory path.</param>
    void PinDeck(Deck deck, string decksPath);

    /// <summary>Unpins a deck by removing the + prefix.</summary>
    /// <param name="deck">The deck to unpin (FileName is updated in-place).</param>
    /// <param name="decksPath">The decks directory path.</param>
    void UnpinDeck(Deck deck, string decksPath);
}