using System;

namespace NextLearn.Desktop.Services;

public interface IDeckFileService
{
    void ArchiveDeck(Guid deckId, string decksPath);

    void UnarchiveDeck(Guid deckId, string decksPath);

    void PinDeck(Guid deckId, string decksPath);

    void UnpinDeck(Guid deckId, string decksPath);
}
