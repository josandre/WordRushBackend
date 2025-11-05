using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;
using WordRush.Core.Features.Scoring.Models;

namespace WordRush.Core.Features.Scoring
{
  public class StopGameScoringService(IHttpClientFactory httpClientFactory, IConfiguration config) : IScoringService
  {
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string ollamaModel = config["Ollama:Model"] ?? "llama3.1";
    private readonly string ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434/api/generate";

    // ----------------------------------------------------------
    // BACKEND VALIDATION RULES
    // ----------------------------------------------------------
    private static void ValidateBasicRules(StopGameRequest request)
    {
      if (string.IsNullOrWhiteSpace(request.Letter))
      {
        throw new ArgumentException("Letter cannot be empty.");
      }

      if (request.Categories == null || request.Categories.Count == 0)
      {
        throw new ArgumentException("Categories cannot be empty.");
      }

      if (request.Players == null || request.Players.Count == 0)
      {
        throw new ArgumentException("Players cannot be empty.");
      }
    }

    private void ApplyBackendRules(StopGameRequest original, StopGameResponse parsed)
    {
      string letter = original.Letter.ToUpperInvariant();

      Dictionary<string, List<string>> allAnswers = [];
      foreach (string category in original.Categories)
      {
        allAnswers[category] = [..original.Players
          .Select(p => p.Answers.ContainsKey(category) ? p.Answers[category]?.Trim() ?? string.Empty : string.Empty)
          .Where(a => !string.IsNullOrEmpty(a))
          .Select(a => a.ToLowerInvariant())];
      }

      foreach (PlayerResult player in parsed.Players)
      {
        foreach (string category in parsed.Categories)
        {
          string answer = player.Answers.ContainsKey(category)
            ? player.Answers[category]?.Trim() ?? string.Empty
            : string.Empty;

          CategoryScore score = player.Scores.ContainsKey(category)
            ? player.Scores[category]
            : new CategoryScore { Points = 0, Reason = "Missing from model output" };

          if (string.IsNullOrWhiteSpace(answer))
          {
            score.Points = 0;
            score.Reason = "No answer provided";
          }
          else if (answer.Length == 1)
          {
            score.Points = 0;
            score.Reason = "Single letter only";
          }
          else if (!answer.StartsWith(letter, StringComparison.OrdinalIgnoreCase))
          {
            score.Points = 0;
            score.Reason = $"Does not start with the letter '{letter}'";
          }
          else
          {
            int count = allAnswers[category].Count(a => a == answer.ToLowerInvariant());
            if (count > 1)
            {
              if (score.Points > 0)
              {
                score.Points = 5;
                score.Reason = "Valid word but duplicated by another player";
              }
            }
            else if (score.Points > 0)
            {
              score.Points = 10;
              score.Reason = "Valid and unique word";
            }
          }

          player.Scores[category] = score;
        }

        player.Total = player.Scores.Values.Sum(s => s.Points);
      }
    }

    // ----------------------------------------------------------
    // MAIN ENTRY POINT
    // ----------------------------------------------------------
    public async Task<StopGameResponse?> ScoreGameAsync(StopGameRequest request)
    {
      ValidateBasicRules(request);

      string prompt = BuildPrompt(request);
      string modelOutput = await SendToModelAsync(prompt);

      Log.Information("Ollama raw output : \n{Output}", modelOutput);

      StopGameResponse? parsed = ParseResponse(modelOutput, request);

      if (parsed == null)
      {
        Log.Warning("Ollama returned no structured output. Returning fallback response.");

        StopGameResponse fallback = new()
        {
          Letter = request.Letter,
          Categories = request.Categories,
          Players = []
        };

        foreach (PlayerEntry player in request.Players)
        {
          PlayerResult result = new()
          {
            Name = player.Name,
            Answers = player.Answers,
            Scores = []
          };

          foreach (string category in request.Categories)
          {
            result.Scores[category] = new CategoryScore
            {
              Points = 0,
              Reason = "No Ollama scoring available"
            };
          }

          result.Total = result.Scores.Values.Sum(s => s.Points);
          fallback.Players.Add(result);
        }

        return fallback;
      }

      ApplyBackendRules(request, parsed);
      return parsed;
    }

    // ----------------------------------------------------------
    // PROMPT BUILDER (Semantic Validation Only)
    // ----------------------------------------------------------
    public string BuildPrompt(StopGameRequest request)
    {
      StringBuilder sb = new();

      _ = sb.AppendLine("You are a strict JSON scoring engine for the word game STOP.");
      _ = sb.AppendLine("You will receive a list of players, categories, and their answers.");
      _ = sb.AppendLine("Your task is to determine if each answer is a real existing word or name that fits its category meaning.");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Rules for Evaluation");
      _ = sb.AppendLine("1. Do NOT check if the word starts with the given letter, or if it’s duplicated. The backend enforces those.");
      _ = sb.AppendLine("2. Assign points only based on semantic correctness and existence:");
      _ = sb.AppendLine("   - If the word exists and fits the category → { points: 10, reason: 'Valid word for category' }.");
      _ = sb.AppendLine("   - If the word exists but clearly does NOT fit the category meaning → { points: 0, reason: 'Word does not fit category meaning' }.");
      _ = sb.AppendLine("   - If the word is invented, nonsense, or not an existing word → { points: 0, reason: 'Word does not exist or is invalid' }.");
      _ = sb.AppendLine("3. Treat fictional or mythological names as valid if they are well-known and fit the context of the category. For example:");
      _ = sb.AppendLine("   - Category: 'Famous Person' → Accept 'Clark Kent', 'Sherlock Holmes', 'Darth Vader'.");
      _ = sb.AppendLine("   - Category: 'Animal' → Reject human names, but accept real species like 'Cat', 'Cheetah'.");
      _ = sb.AppendLine("4. Always include a clear reason for each answer’s score.");
      _ = sb.AppendLine("5. Return only valid JSON with no commentary or markdown.");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Output Format");
      _ = sb.AppendLine("{");
      _ = sb.AppendLine("  \"letter\": string,");
      _ = sb.AppendLine("  \"categories\": string[],");
      _ = sb.AppendLine("  \"players\": [");
      _ = sb.AppendLine("    {");
      _ = sb.AppendLine("      \"name\": string,");
      _ = sb.AppendLine("      \"answers\": { \"<Category>\": \"<Answer>\", ... },");
      _ = sb.AppendLine("      \"scores\": { \"<Category>\": { \"points\": number, \"reason\": string }, ... },");
      _ = sb.AppendLine("      \"total\": number");
      _ = sb.AppendLine("    }");
      _ = sb.AppendLine("  ]");
      _ = sb.AppendLine("}");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Notes");
      _ = sb.AppendLine("- Be neutral, concise, and consistent.");
      _ = sb.AppendLine("- Always keep the same number of categories and players as provided.");
      _ = sb.AppendLine("- Do not modify or correct the answers.");
      _ = sb.AppendLine("- Compute the 'total' for each player as the sum of all their category points.");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Input Data");
      _ = sb.AppendLine(JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));
      _ = sb.AppendLine();
      _ = sb.AppendLine("Return ONLY valid JSON following the exact format above.");
      _ = sb.AppendLine("Every 'answers' and 'scores' field must be a dictionary with one entry per category.");
      foreach (PlayerEntry player in request.Players)
      {
        _ = sb.AppendLine($"- {player.Name}: {JsonSerializer.Serialize(player.Answers)}");
      }

      return sb.ToString();
    }

    // ----------------------------------------------------------
    // SEND PROMPT TO OLLAMA
    // ----------------------------------------------------------
    public async Task<string> SendToModelAsync(string _prompt)
    {
      try
      {
        var body = new
        {
          model = ollamaModel,
          prompt = _prompt,
          stream = false,
          options = new { temperature = 0.2, num_ctx = 4096 }
        };

        StringContent content = new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync(ollamaBaseUrl, content);

        if (!response.IsSuccessStatusCode)
        {
          string err = await response.Content.ReadAsStringAsync();
          Log.Error("Ollama API returned error: {Status} - {Message}", response.StatusCode, err);
          return string.Empty;
        }

        string json = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("response", out JsonElement inner))
        {
          string innerText = inner.GetString() ?? string.Empty;
          int firstBrace = innerText.IndexOf('{');
          if (firstBrace >= 0)
          {
            innerText = innerText[firstBrace..];
          }

          return innerText.Trim();
        }

        Log.Warning("Ollama response did not contain a 'response' field.");
        return string.Empty;
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Error calling Ollama API");
        return string.Empty;
      }
    }

    // ----------------------------------------------------------
    // PARSE OLLAMA RESPONSE
    // ----------------------------------------------------------
    public StopGameResponse? ParseResponse(string responseText, StopGameRequest originalRequest)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(responseText))
        {
          Log.Warning("Ollama response is empty.");
          return null;
        }

        // Step 1: Extract valid JSON block
        int firstBrace = responseText.IndexOf('{');
        int lastBrace = responseText.LastIndexOf('}');
        if (firstBrace == -1 || lastBrace == -1 || lastBrace <= firstBrace)
        {
          Log.Warning("No valid JSON braces found in Ollama output.");
          return null;
        }

        string jsonOnly = responseText.Substring(firstBrace, lastBrace - firstBrace + 1);

        // Step 2: Sanitize obvious bad characters (e.g., /Brazil/ -> "Brazil")
        jsonOnly = System.Text.RegularExpressions.Regex.Replace(
                    jsonOnly,
                    @"(?<=:\s*)/([^/]+)/",
                    "\"$1\""
                  );

        // Step 3: Trim markdown markers or accidental text from LLM
        int jsonStart = jsonOnly.IndexOf('{');
        int jsonEnd = jsonOnly.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
          jsonOnly = jsonOnly.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        // Step 4: Attempt to deserialize
        StopGameResponse? parsed = JsonSerializer.Deserialize<StopGameResponse>(
          jsonOnly,
          new JsonSerializerOptions
          {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
          });

        if (parsed == null)
        {
          Log.Warning("Deserialized response is null.");
          return null;
        }

        // Step 5: Rebuild missing fields
        if (parsed.Categories == null || parsed.Categories.Count == 0)
        {
          parsed.Categories = [.. originalRequest.Categories];
        }

        if (string.IsNullOrWhiteSpace(parsed.Letter))
        {
          parsed.Letter = originalRequest.Letter;
        }

        foreach (PlayerResult player in parsed.Players)
        {
          player.Answers ??= [];

          player.Scores ??= [];

          foreach (string category in parsed.Categories)
          {
            if (!player.Scores.ContainsKey(category))
            {
              player.Scores[category] = new CategoryScore
              {
                Points = 0,
                Reason = "Missing from model output"
              };
            }
          }

          player.Total = player.Scores.Values.Sum(s => s.Points);
        }

        return parsed;
      }
      catch (JsonException jex)
      {
        Log.Error(jex, "JSON parse error when decoding Ollama response.");
        Log.Error("Ollama raw JSON before failure:\n{Raw}", responseText);
        return null;
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Unexpected error while parsing Ollama response.");
        return null;
      }
    }

  }
}
