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

public partial class TagInferenceService : ITagInferenceService
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

    public TagInferenceService(HttpClient http)
    {
        _http = http;
    }

    public async Task<TagInferenceResult> InferTagsAsync(string deckContent, string existingTags, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(deckContent);

        const long maxSafeTokens = 700_000;
        var estimatedTokens = deckContent.Length / 4L;

        if (estimatedTokens > 900_000)
        {
            return new TagInferenceResult
            {
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

        var prompt = $@"You are a tag inference AI. Given this deck content and its existing tags, suggest 2–15 additional tags that capture the core concepts, topics, semantics, mnemonics, and potential aliases.

Existing tags: {existingTags}

Deck content:
{content}

Return a JSON array of strings only, e.g. [""tag1"", ""tag2""].";

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
            var result = await TryModelWithRetriesAsync(model, json, apiKey).ConfigureAwait(false);
            lastErrBody = result.LastErrBody;

            if (result.FinalResult != null)
            {
                return result.FinalResult;
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
                var result = await TryModelWithRetriesAsync(discovered, json, apiKey).ConfigureAwait(false);
                lastErrBody = result.LastErrBody;

                if (result.FinalResult != null)
                {
                    return result.FinalResult;
                }
            }
        }

        return new TagInferenceResult
        {
            Success = false,
            Error = lastErrBody != null
                ? $"Gemini API error: {lastErrBody}"
                : "All available models failed. Try again later.",
        };
    }

    private async Task<ModelResult> TryModelWithRetriesAsync(string model, string json, string apiKey)
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
                    var parsed = ParseSuccessfulResponse(responseBody);
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
                    new TagInferenceResult
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
                    new TagInferenceResult
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
                    new TagInferenceResult
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
                    new TagInferenceResult
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

    private static TagInferenceResult? ParseSuccessfulResponse(string responseBody)
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
            return new TagInferenceResult
            {
                Success = false,
                Error = "Gemini returned an empty response. Try again.",
            };
        }

        // Try strict JSON array parse
        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(text);
            if (tags != null && tags.Count > 0)
            {
                return new TagInferenceResult
                {
                    SuggestedTags = tags.Select(t => t.Replace(' ', '_')).ToList(),
                    Success = true,
                };
            }
        }
        catch (JsonException)
        {
            // Fall through to fallback parsing
        }

        // Fallback: try to extract tags from non-JSON formats
        var extracted = ExtractTags(text);
        if (extracted.Count > 0)
        {
            return new TagInferenceResult
            {
                SuggestedTags = extracted.Select(t => t.Replace(' ', '_')).ToList(),
                Success = true,
            };
        }

        return new TagInferenceResult
        {
            Success = false,
            Error = $"Gemini returned unexpected format. Raw response (truncated): {Truncate(text, 500)}",
        };
    }

    private static List<string> ExtractTags(string text)
    {
        // Try markdown code block containing JSON
        var codeBlockMatch = CodeBlockRegex().Match(text);
        if (codeBlockMatch.Success)
        {
            var inner = codeBlockMatch.Groups[1].Value.Trim();
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(inner);
                if (parsed != null && parsed.Count > 0)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        // Line-by-line: each non-empty line is a tag
        var result = new List<string>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('-') || trimmed.StartsWith('*'))
            {
                trimmed = trimmed[1..].Trim();
            }

            trimmed = trimmed.Trim('"', '\'', '[', ']', ',', ' ');
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length > 1)
            {
                result.Add(trimmed);
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        // Comma-separated fallback
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim('"', '\'', '[', ']', ' '))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?(.+?)\n?```", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    private record ModelResult(TagInferenceResult? FinalResult, string? LastErrBody, bool Was404);

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
