using System.Threading.Tasks;

namespace NextLearn.Desktop.Services;

public interface IFlashcardService
{
    Task<FlashcardResult> GenerateFlashcardsAsync(string deckContent, string apiKey, FlashcardGenerationMode mode);
}
