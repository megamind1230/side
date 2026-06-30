using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NextLearn.Desktop.Services;

public partial class FlashcardService : IFlashcardService
{
    private readonly HttpClient _http;
    private string? _cachedModelName;

    private static readonly string[] KnownModels =
    [
        "gemini-2.5-flash",
        "gemini-2.0-flash",
        "gemini-flash-latest",
        "gemini-1.5-flash",
    ];

    private static readonly int[] RetryDelays = [1, 5, 10, 20, 40, 80];

    public FlashcardService(HttpClient http)
    {
        _http = http;
    }

    public async Task<FlashcardResult> GenerateFlashcardsAsync(string deckContent, string apiKey, FlashcardGenerationMode mode)
    {
        ArgumentNullException.ThrowIfNull(deckContent);

        const long maxSafeTokens = 700_000;
        var estimatedTokens = deckContent.Length / 4L;

        if (estimatedTokens > 900_000)
        {
            return new FlashcardResult
            {
                Mode = mode,
                Success = false,
                Error = $"Deck too large (estimated ~{estimatedTokens} tokens, max 1M). Split into smaller files.",
            };
        }

        string content;
        if (estimatedTokens > maxSafeTokens)
        {
            var truncChars = (int)(maxSafeTokens * 4);
            content = deckContent[..truncChars];
        }
        else
        {
            content = deckContent;
        }

        var prompt = mode == FlashcardGenerationMode.Basic ? BuildBasicPrompt(content) : BuildClozePrompt(content);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var modelQueue = new List<string>();
        if (_cachedModelName != null)
        {
            modelQueue.Add(_cachedModelName);
        }

        modelQueue.AddRange(KnownModels);

        string? lastErrBody = null;
        bool allWere404 = true;

        foreach (var model in modelQueue)
        {
            var result = await TryModelWithRetriesAsync(model, json, apiKey, mode).ConfigureAwait(false);
            lastErrBody = result.LastErrBody;

            if (result.FinalResult != null)
            {
                var r = result.FinalResult;
                r.Mode = mode;
                return r;
            }

            if (!result.Was404)
            {
                allWere404 = false;
            }
        }

        // All known models returned 404 — try to discover via ListModels
        if (allWere404 && _cachedModelName == null)
        {
            var discovered = await DiscoverModelAsync(apiKey).ConfigureAwait(false);
            if (discovered != null)
            {
                _cachedModelName = discovered;
                var result = await TryModelWithRetriesAsync(discovered, json, apiKey, mode).ConfigureAwait(false);
                lastErrBody = result.LastErrBody;

                if (result.FinalResult != null)
                {
                    var r = result.FinalResult;
                    r.Mode = mode;
                    return r;
                }
            }
        }

        return new FlashcardResult
        {
            Mode = mode,
            Success = false,
            Error = lastErrBody != null
                ? $"Gemini API error: {lastErrBody}"
                : "All available models failed. Try again later.",
        };
    }

    private static string BuildBasicPrompt(string content)
    {
        return $@"You are an Anki flashcard generator. Convert the following study material into BASIC flashcards in TSV format.

BASIC flashcards are simple question → answer pairs (one per line, tab-separated).

Use these for:
- Vocabulary: term TAB translation (e.g. ""hello	bonjour"")
- Definitions: term TAB definition (e.g. ""Function	A reusable block of code that performs a specific task"")
- Q&A: question TAB answer

Rules:
- One card per line, tab-separated (term TAB definition/answer)
- Return ONLY the TSV lines — no markdown, no code fences, no explanations
- Cover EVERY section of the content — do not skip any part
- Generate as many cards as needed to cover all material

Deck content:
---
{content}
---";
    }

    private static string BuildClozePrompt(string content)
    {
        return $@"You are an Anki flashcard generator. Convert the following study material into CLOZE deletion flashcards.

CLOZE cards use {{{{c1::term}}}} to create fill-in-the-blank sentences.

Use these for:
- Definitions: ""A {{{{c1::function}}}} is a reusable block of code that performs a specific task.""
- Key concepts: ""Binary search has a time complexity of {{{{c1::O(log n)}}}}.""
- Fill-in-the-blank for any term, definition, or concept being explained

Format: One sentence per line, each sentence containing exactly one {{{{c1::term}}}} blank.

Rules:
- Every line MUST contain {{{{c1::}}}} syntax — one blank per card
- Do NOT use tab characters — the entire line is the cloze text
- Return ONLY the cloze lines — no markdown, no code fences, no explanations
- Cover EVERY section of the content — do not skip any part
- Generate as many cards as needed to cover all material

Deck content:
---
{content}
---";
    }

    private async Task<ModelResult> TryModelWithRetriesAsync(string model, string json, string apiKey, FlashcardGenerationMode mode)
    {
        string? lastErrBody = null;

        for (int attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}",
                    httpContent).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var parsed = ParseSuccessfulResponse(responseBody, mode);
                    return new ModelResult(parsed, lastErrBody, Was404: false);
                }

                var errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                lastErrBody = errBody;

                if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    if (attempt < RetryDelays.Length - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(RetryDelays[attempt])).ConfigureAwait(false);
                        continue;
                    }

                    return new ModelResult(null, lastErrBody, Was404: false);
                }

                if (response.StatusCode == (System.Net.HttpStatusCode)404)
                {
                    return new ModelResult(null, lastErrBody, Was404: true);
                }

                return new ModelResult(
                    new FlashcardResult
                    {
                        Success = false,
                        Error = response.StatusCode switch
                        {
                            System.Net.HttpStatusCode.Unauthorized => $"Invalid API key. Response: {errBody}",
                            _ => $"Gemini API error ({(int)response.StatusCode}): {errBody}",
                        },
                    },
                    lastErrBody,
                    Was404: false);
            }
            catch (HttpRequestException)
            {
                return new ModelResult(
                    new FlashcardResult
                    {
                        Success = false,
                        Error = "No internet connection. Check and try again.",
                    },
                    null,
                    Was404: false);
            }
            catch (TaskCanceledException)
            {
                return new ModelResult(
                    new FlashcardResult
                    {
                        Success = false,
                        Error = "Request timed out. Check your internet connection and try again.",
                    },
                    null,
                    Was404: false);
            }
            catch (JsonException)
            {
                return new ModelResult(
                    new FlashcardResult
                    {
                        Success = false,
                        Error = "Failed to parse Gemini response. Try again.",
                    },
                    null,
                    Was404: false);
            }
        }

        return new ModelResult(null, lastErrBody, Was404: false);
    }

    private static FlashcardResult? ParseSuccessfulResponse(string responseBody, FlashcardGenerationMode mode)
    {
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var text = geminiResponse?.Candidates?.FirstOrDefault()
            ?.Content?.Parts?.FirstOrDefault()
            ?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return new FlashcardResult
            {
                Success = false,
                Error = "Gemini returned an empty response. Try again.",
            };
        }

        // Strip any markdown code fence if present
        var codeBlockMatch = CodeBlockRegex().Match(text);
        if (codeBlockMatch.Success)
        {
            text = codeBlockMatch.Groups[1].Value.Trim();
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<string> validLines;
        string missingFormat;

        if (mode == FlashcardGenerationMode.Basic)
        {
            validLines = lines.Where(l => l.Contains('\t')).ToList();
            missingFormat = "tab-separated";
        }
        else
        {
            validLines = lines.Where(l => l.Contains("{{c1::")).ToList();
            missingFormat = "cloze ({{c1::}})";
        }

        if (validLines.Count == 0)
        {
            return new FlashcardResult
            {
                Success = false,
                Error = $"Gemini did not return valid {missingFormat} flashcards. Raw response:\n{text}",
            };
        }

        return new FlashcardResult
        {
            Content = string.Join("\n", validLines),
            Count = validLines.Count,
            Success = true,
        };
    }

    [GeneratedRegex(@"```(?:tsv|text|plain)?\s*\n?(.+?)\n?```", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    private record ModelResult(FlashcardResult? FinalResult, string? LastErrBody, bool Was404);

    private async Task<string?> DiscoverModelAsync(string apiKey)
    {
        try
        {
            var response = await _http.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}")
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<ModelsListResponse>(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            var model = list?.Models?
                .Where(m => m.SupportedGenerationMethods?.Contains("generateContent") == true)
                .Where(m => m.Name?.Contains("flash", StringComparison.OrdinalIgnoreCase) == true)
                .OrderByDescending(m => m.Version ?? string.Empty)
                .FirstOrDefault();

            model ??= list?.Models?
                .FirstOrDefault(m => m.SupportedGenerationMethods?.Contains("generateContent") == true);

            if (model?.Name == null)
            {
                return null;
            }

            return model.Name.StartsWith("models/") ? model.Name[7..] : model.Name;
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }

    private class ModelsListResponse
    {
        [JsonPropertyName("models")]
        public List<ModelInfo>? Models { get; set; }
    }

    private class ModelInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("supportedGenerationMethods")]
        public List<string>? SupportedGenerationMethods { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
