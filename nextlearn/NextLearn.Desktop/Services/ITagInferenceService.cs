using System.Threading.Tasks;

namespace NextLearn.Desktop.Services;

public interface ITagInferenceService
{
    Task<TagInferenceResult> InferTagsAsync(string deckContent, string existingTags, string apiKey);
}
