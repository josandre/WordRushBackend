using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;
using WordRush.Core.Features.Scoring.Models;

namespace WordRush.Core.Features.Scoring
{
  public class StopGameScoringService : IScoringService
  {
    private const string BASEURL = "http://localhost:11434/api/generate";
    private readonly HttpClient httpClient;
    private readonly string ollamaModel;
    private readonly string ollamaBaseUrl;
    private readonly JsonSerializerOptions jsonOpts = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = false
    };

    private readonly object modelOptions;
    private readonly TimeSpan ollamaRequestTimeout;

    public StopGameScoringService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
      httpClient = httpClientFactory.CreateClient();
      httpClient.Timeout = Timeout.InfiniteTimeSpan; // disable automatic cancellation

      ollamaModel = config["Ollama:Model"] ?? "llama3";
      ollamaBaseUrl = config["Ollama:BaseUrl"] ?? BASEURL;

      int timeoutSeconds = int.TryParse(config["Ollama:RequestTimeoutSeconds"], out int t) ? t : 300;
      ollamaRequestTimeout = TimeSpan.FromSeconds(timeoutSeconds);

      modelOptions = new
      {
        temperature = double.TryParse(config["Ollama:Options:temperature"], out double temp) ? temp : 0.15,
        num_predict = int.TryParse(config["Ollama:Options:num_predict"], out int np) ? np : 2048
      };
    }

    // Warm-up function called from Program.cs
    public async Task WarmUpModelAsync()
    {
      try
      {
        var warmupPrompt = new
        {
          model = ollamaModel,
          prompt = "Return JSON { \"status\": \"ready\" }",
          stream = false,
          options = modelOptions
        };

        using StringContent content = new(
            JsonSerializer.Serialize(warmupPrompt, jsonOpts),
            Encoding.UTF8,
            "application/json");

        Log.Information("Sending Ollama warm-up request to {Url}...", ollamaBaseUrl);
        using HttpResponseMessage response = await httpClient.PostAsync(ollamaBaseUrl, content);
        string result = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
          Log.Information("Ollama warm-up succeeded. Response: {Snippet}", result[..Math.Min(result.Length, 100)]);
        }
        else
        {
          Log.Warning("Ollama warm-up failed. Status: {Status} | Message: {Message}", response.StatusCode, result);
        }
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "Warm-up skipped — Ollama may not be reachable.");
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
                    "\"$1\"");

        // Step 3: Fix phi3 pseudo-JSON (single quotes instead of double quotes)
        jsonOnly = System.Text.RegularExpressions.Regex.Replace(
                    jsonOnly,
                    @"'([^']*)'",
                    "\"$1\"");

        // Step 4: Remove trailing commas before closing braces/brackets
        jsonOnly = System.Text.RegularExpressions.Regex.Replace(
                    jsonOnly,
                    @",(\s*[}\]])",
                    "$1");

        // Step 5: Trim markdown markers or accidental text from LLM
        int jsonStart = jsonOnly.IndexOf('{');
        int jsonEnd = jsonOnly.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
          jsonOnly = jsonOnly.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        // Step 6: Clean invisible characters (BOM, etc.)
        jsonOnly = jsonOnly.Replace("\uFEFF", string.Empty).Trim();

        // Step 6b: Repair malformed model output (e.g., stray "0- points:" lines)
        jsonOnly = System.Text.RegularExpressions.Regex.Replace(
                    jsonOnly,
                    @"\b0-\s*points\s*:\s*\d+,\s*reason\s*:\s*'[^']*'\s*,?",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Ensure missing braces are balanced (defensive fix)
        int open = jsonOnly.Count(c => c == '{');
        int close = jsonOnly.Count(c => c == '}');
        if (close < open)
        {
          jsonOnly += new string('}', open - close);
        }

        // Step 7: Attempt to deserialize
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

        // Step 8: Rebuild missing fields
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

          parsed.Categories
          .Where(category => !player.Scores.ContainsKey(category))
          .ToList()
          .ForEach(category =>
              player.Scores[category] = new CategoryScore
              {
                Points = 0,
                Reason = "Missing from model output"
              });

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
      _ = sb.AppendLine("The model temperature is low (≈ 0.15), so prefer factual correctness over uncertainty — if unsure about a name or word, mark it valid only if it is widely known in reality or fiction.");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Rules for Evaluation");
      _ = sb.AppendLine("1. Do NOT check if the word starts with the given letter, or if it’s duplicated. The backend enforces those.");
      _ = sb.AppendLine("2. Assign points only based on semantic correctness and existence:");
      _ = sb.AppendLine("   - If the word exists and fits the category → { points: 10, reason: 'Valid word for category' }.");
      _ = sb.AppendLine("   - If the word exists but clearly does NOT fit the category meaning → { points: 0, reason: 'Word does not fit category meaning' }.");
      _ = sb.AppendLine("   - If the word is invented, nonsense, or not an existing word → { points: 0, reason: 'Word does not exist or is invalid' }.");
      _ = sb.AppendLine("3. Treat fictional or mythological names as valid if they are well-known or culturally significant (e.g. 'Sherlock Holmes', 'Darth Vader', 'Cersei Lannister', 'Frodo', 'Zeus', 'Athena').");
      _ = sb.AppendLine("   - Reject obscure or nonsense invented names that do not appear in notable fiction, mythology, or real-world use.");
      _ = sb.AppendLine("   - Category: 'Animal' → Reject human names, but accept real species like 'Cat', 'Cheetah'.");
      _ = sb.AppendLine("4. Only consider real sovereign countries (e.g. France, Brazil, Japan).");
      _ = sb.AppendLine("5. Always include a clear reason for each answer’s score.");
      _ = sb.AppendLine("   - Multi-word food items are valid (e.g. \"chocolate cake\", \"apple pie\", \"ice cream\").\r\n");
      _ = sb.AppendLine("6. Return only valid JSON with no commentary or markdown.");
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
      _ = sb.AppendLine("    }");
      _ = sb.AppendLine("  ]");
      _ = sb.AppendLine("}");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Notes");
      _ = sb.AppendLine("- Be neutral, concise, and consistent.");
      _ = sb.AppendLine("- Always keep the same number of categories and players as provided.");
      _ = sb.AppendLine("- Do not modify or correct the answers.");
      _ = sb.AppendLine("- DO NOT Compute totals; backend will handle them.");
      _ = sb.AppendLine();
      _ = sb.AppendLine("### Input Data");
      _ = sb.AppendLine(JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));
      _ = sb.AppendLine();
      _ = sb.AppendLine("Return ONLY valid JSON following the exact format above.");
      _ = sb.AppendLine("Every 'answers' and 'scores' field must be a dictionary with one entry per category.");
      _ = sb.AppendLine("### Structural Enforcement");
      _ = sb.AppendLine("- Every entry in the 'answers' block MUST be a plain string, e.g.:");
      _ = sb.AppendLine("    \"Programming Language\": \"Python\"");
      _ = sb.AppendLine("  NEVER an object, array, or nested element.");
      _ = sb.AppendLine("- Only the 'scores' block may contain objects with 'points' and 'reason'.");
      _ = sb.AppendLine("- Violating this format makes the JSON invalid and will cause parsing failure.");

      foreach (PlayerEntry player in request.Players)
      {
        _ = sb.AppendLine($"- {player.Name}: {JsonSerializer.Serialize(player.Answers)}");
      }

      return sb.ToString();
    }

    // ----------------------------------------------------------
    // SEND PROMPT TO OLLAMA
    // ----------------------------------------------------------
    // Main call to Ollama with manual timeout and full logging
    public async Task<string> SendToModelAsync(string prompt)
    {
      Stopwatch timer = Stopwatch.StartNew();
      int estimatedPromptTokens = EstimateTokenCount(prompt);

      try
      {
        var payload = new
        {
          model = ollamaModel,
          prompt,
          stream = false,
          options = modelOptions
        };

        using StringContent content = new(
            JsonSerializer.Serialize(payload, jsonOpts),
            Encoding.UTF8,
            "application/json");

        using CancellationTokenSource cts = new(ollamaRequestTimeout);

        Log.Information(
          "Sending prompt to Ollama model '{Model}' (timeout {Timeout}s)...", ollamaModel, ollamaRequestTimeout.TotalSeconds);

        using HttpResponseMessage response = await httpClient.PostAsync(ollamaBaseUrl, content, cts.Token);
        timer.Stop();

        string responseText = await response.Content.ReadAsStringAsync();
        int estimatedResponseTokens = EstimateTokenCount(responseText);

        Log.Information(
          "Ollama call finished in {Elapsed} ms | Prompt ≈ {PromptTokens} tokens | Response ≈ {ResponseTokens} tokens",
          timer.ElapsedMilliseconds,
          estimatedPromptTokens,
          estimatedResponseTokens);

        if (!response.IsSuccessStatusCode)
        {
          Log.Error("Ollama API error {Status}: {Message}", response.StatusCode, responseText);
          return string.Empty;
        }

        using JsonDocument doc = JsonDocument.Parse(responseText);
        if (doc.RootElement.TryGetProperty("response", out JsonElement inner))
        {
          string result = inner.GetString() ?? string.Empty;
          int firstBrace = result.IndexOf('{');
          if (firstBrace >= 0)
          {
            result = result[firstBrace..];
          }

          if (timer.ElapsedMilliseconds > 10000)
          {
            Log.Warning("Ollama took unusually long ({Elapsed} ms) for this request.", timer.ElapsedMilliseconds);
          }

          Log.Information(
            "Total ≈ {TotalTokens} tokens processed in {Elapsed} ms", estimatedPromptTokens + estimatedResponseTokens, timer.ElapsedMilliseconds);

          return result.Trim();
        }

        Log.Warning("Ollama response missing 'response' field.");
        return string.Empty;
      }
      catch (TaskCanceledException ex)
      {
        timer.Stop();
        Log.Warning(ex, "Ollama call exceeded manual timeout ({Timeout}s) after {Elapsed} ms.", ollamaRequestTimeout.TotalSeconds, timer.ElapsedMilliseconds);
        return string.Empty;
      }
      catch (Exception ex)
      {
        timer.Stop();
        Log.Error(ex, "Error calling Ollama API after {Elapsed} ms", timer.ElapsedMilliseconds);
        return string.Empty;
      }
    }

    private static int EstimateTokenCount(string text)
    {
      return Math.Max(1, text.Length / 4);
    }

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
  }
}
