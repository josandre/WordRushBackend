using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Serilog;
using WordRush.Core.Features.Scoring.Models;

namespace WordRush.Core.Features.Scoring
{
  public class StopGameScoringService : IScoringService
  {
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
      ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434/api/generate";

      int timeoutSeconds = int.TryParse(config["Ollama:RequestTimeoutSeconds"], out int t) ? t : 300;
      ollamaRequestTimeout = TimeSpan.FromSeconds(timeoutSeconds);

      modelOptions = new
      {
        temperature = double.TryParse(config["Ollama:Options:temperature"], out double temp) ? temp : 0.15,
        num_predict = int.TryParse(config["Ollama:Options:num_predict"], out int np) ? np : 1024
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

    // Normalize multilingual field names (Spanish → English)
    private static string NormalizeJsonKeys(string json)
    {
      // Replace only property names, not values
      json = Regex.Replace(json, @"\bCategor[ií]as\b", "categories", RegexOptions.IgnoreCase);
      json = Regex.Replace(json, @"\bJugadores\b", "players", RegexOptions.IgnoreCase);
      json = Regex.Replace(json, @"\bNombre\b", "name", RegexOptions.IgnoreCase);
      json = Regex.Replace(json, @"\bRespuestas\b", "answers", RegexOptions.IgnoreCase);
      json = Regex.Replace(json, @"\bPuntajes\b", "scores", RegexOptions.IgnoreCase);
      json = Regex.Replace(json, @"\bpuntos\b", "points", RegexOptions.IgnoreCase);
      json = Regex.Replace(json, @"\braz[oó]n\b", "reason", RegexOptions.IgnoreCase);
      return json;
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

        // Step 1: Extract JSON portion
        int firstBrace = responseText.IndexOf('{');
        int lastBrace = responseText.LastIndexOf('}');
        if (firstBrace == -1 || lastBrace == -1 || lastBrace <= firstBrace)
        {
          Log.Warning("No valid JSON braces found in Ollama output.");
          return null;
        }

        string jsonOnly = responseText.Substring(firstBrace, lastBrace - firstBrace + 1);

        // Step 2: Basic sanitation
        jsonOnly = Regex.Replace(jsonOnly, @"(?<=:\s*)/([^/]+)/", "\"$1\"");
        jsonOnly = Regex.Replace(jsonOnly, @"'([^']*)'", "\"$1\""); // single quotes → double
        jsonOnly = Regex.Replace(jsonOnly, @",(\s*[}\]])", "$1");   // remove trailing commas
        jsonOnly = jsonOnly.Replace("\uFEFF", string.Empty).Trim();

        // Step 3: Defensive repair — malformed nested objects inside "answers"
        // Example: "answers": { "Name": { "points": 10, "reason": "Valid" } }
        string beforeFix = jsonOnly;
        jsonOnly = Regex.Replace(
            jsonOnly,
            @"""Answers""\s*:\s*\{([^{}]*\{[^{}]*\}[^{}]*)\}",
            match =>
            {
              string fixedBlock = Regex.Replace(match.Value, @"\{[^{}]*\}", "\"\"");
              return fixedBlock;
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (!ReferenceEquals(beforeFix, jsonOnly))
        {
          Log.Warning("Detected nested score objects inside 'answers' — auto-cleaned for safe parsing.");
        }

        // Step 4: Balance braces if truncated
        int open = jsonOnly.Count(c => c == '{');
        int close = jsonOnly.Count(c => c == '}');
        if (close < open)
        {
          jsonOnly += new string('}', open - close);
        }

        // Step 4.5: Normalize multilingual JSON keys
        jsonOnly = NormalizeJsonKeys(jsonOnly);

        // Step 5: Deserialize safely
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

        // Step 6: Rebuild missing fields from request
        parsed.Categories ??= new List<string>(originalRequest.Categories);
        parsed.Letter ??= originalRequest.Letter;

        foreach (PlayerResult player in parsed.Players)
        {
          // Normalize player name
          if (!string.IsNullOrWhiteSpace(player.Name))
          {
            player.Name = Regex.Replace(player.Name, @"\s+", string.Empty).Trim();
            player.Name = player.Name.Length > 1
                ? char.ToUpper(player.Name[0]) + player.Name[1..].ToLower()
                : player.Name.ToUpper();
          }

          // Match UserId from JSON response if available, otherwise match by name
          PlayerEntry? matchingRequestPlayer = null;

          // First try to match by UserId if both are available
          if (player.UserId.HasValue)
          {
            matchingRequestPlayer = originalRequest.Players
              .FirstOrDefault(p => p.UserId == player.UserId);

            // Validate that the name also matches if UserId matched
            if (matchingRequestPlayer != null &&
                !string.Equals(matchingRequestPlayer.Name, player.Name, StringComparison.OrdinalIgnoreCase))
            {
              Log.Warning(
                "UserId match found but name mismatch: JSON has '{JsonName}' (UserId: {JsonUserId}) but request has '{RequestName}' (UserId: {RequestUserId}). Using name match instead.",
                player.Name, player.UserId, matchingRequestPlayer.Name, matchingRequestPlayer.UserId);
              matchingRequestPlayer = null; // Reset to force name-based matching
            }
          }

          // Fallback to name-based matching if UserId matching didn't work
          if (matchingRequestPlayer == null)
          {
            string NormalizeName(string s)
            {
              if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

              // remove all whitespace
              s = Regex.Replace(s, @"\s+", "");

              // remove accents
              s = s.Normalize(NormalizationForm.FormD);
              s = new string(s.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

              return s.Trim().ToLowerInvariant();
            }


            // If we matched by name, validate/copy the UserId from request
            if (matchingRequestPlayer == null)
            {
              matchingRequestPlayer = originalRequest.Players
                .FirstOrDefault(p =>
                    NormalizeName(p.Name) == NormalizeName(player.Name)
                );

              if (matchingRequestPlayer != null)
              {
                player.UserId = matchingRequestPlayer.UserId;
              }
              else
              {
                Log.Warning(
                  "Could not match player '{Name}' (UserId: {UserId}) from JSON response to any player in the request.",
                  player.Name, player.UserId);
              }
            }
            else
            {
              Log.Warning(
                "Could not match player '{Name}' (UserId: {UserId}) from JSON response to any player in the request.",
                player.Name, player.UserId);
            }
          }
          else
          {
            // UserId matched successfully, ensure it's set
            player.UserId = matchingRequestPlayer.UserId;
          }

          player.Answers ??= new Dictionary<string, string>();
          player.Scores ??= new Dictionary<string, CategoryScore>();

          // Ensure all categories exist in both maps
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

            if (!player.Answers.ContainsKey(category) || string.IsNullOrWhiteSpace(player.Answers[category]))
            {
              string restored = originalRequest.Players
                  .FirstOrDefault(p =>
                      string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase))?
                  .Answers.GetValueOrDefault(category, string.Empty) ?? string.Empty;

              player.Answers[category] = restored;

              if (string.IsNullOrWhiteSpace(restored))
              {
                Log.Warning("Restored empty answer for {Player} in category '{Category}'", player.Name, category);
              }
              else
              {
                Log.Warning("Restored missing answer for {Player} in category '{Category}' → '{Answer}'", player.Name, category, restored);
              }
            }
          }

          // Calculate total
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

      // --- Pre-filter nonsense and repeated answers before AI call ---
      foreach (var player in request.Players)
      {
        List<string> answers = player.Answers.Values
            .Select(a => a?.Trim().ToLowerInvariant() ?? string.Empty)
            .ToList();

        // If all answers are empty
        if (answers.All(string.IsNullOrWhiteSpace))
        {
          Log.Information("[FILTER] {Player} skipped round with all blanks", player.Name);
          continue;
        }

        // If same word used for all categories
        if (answers.Distinct().Count() == 1)
        {
          Log.Information("[FILTER] {Player} used same word in all categories: {Word}", player.Name, answers.First());
          foreach (string category in request.Categories)
          {
            player.Answers[category] = answers.First();
          }
        }

        // Sanitize: collapse duplicate spaces, remove punctuation
        foreach (string key in player.Answers.Keys.ToList())
        {
          string ans = player.Answers[key];
          ans = Regex.Replace(ans, @"[^a-zA-ZáéíóúüñÁÉÍÓÚÜÑ\s-]", string.Empty).Trim();
          player.Answers[key] = ans;
        }
      }


      string prompt = BuildPrompt(request);
      // works with ollama descomment when ollama can run and use it instead of the JSON
      string modelOutput = await SendToModelAsync(prompt, request);

#region JSON WORKAROUND

      // Read from JSON file instead of calling the model
      //string jsonFilePath = FindJsonFile();
      //string modelOutput = File.Exists(jsonFilePath)
      //  ? await File.ReadAllTextAsync(jsonFilePath)
      //  : throw new FileNotFoundException($"Could not find sample-response.json file. Tried: {jsonFilePath}");

      //Log.Information("Reading from JSON file: {FilePath}", jsonFilePath);
#endregion

      Log.Information("JSON file content : \n{Output}", modelOutput);

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
            UserId = player.UserId,
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
      StringBuilder builder = new();

      _ = builder.AppendLine("You are a strict JSON scoring engine for the word game STOP.");
      _ = builder.AppendLine("You receive a list of players, categories, and their answers.");
      _ = builder.AppendLine("Your task is to assign scores to each answer according to the rules, include the original answers, and include the categories list in the output.");
      _ = builder.AppendLine();

      // Scoring rules
      _ = builder.AppendLine("Rules:");
      _ = builder.AppendLine("1. IMPORTANT! Only words that start with the given letter are valid. Ignore capitalization and accents when comparing the first letter.");
      _ = builder.AppendLine("2. IMPORTANT! The word must match the category meaning exactly (e.g. an animal must be a real species name, a country must be a real country or city).");
      _ = builder.AppendLine("3. IMPORTANT! The word MUST be a real word in any widely spoken language (English, Spanish, Italian, German, Portuguese, etc.) that matches the category meaning. Single letters, random strings, abbreviations or nonsense words are always invalid.");
      _ = builder.AppendLine("4. Valid answers always score 10 points. Invalid, missing, or wrong‑letter answers always score 0 points. Do not assign duplicate penalties here (the backend will handle duplicates).");
      _ = builder.AppendLine("5. The top-level JSON object must include 'letter', 'categories', and 'players'.");
      _ = builder.AppendLine();

      // Clarifications for the Name category
      _ = builder.AppendLine("IMPORTANT: The category \"Name\" refers ONLY to the player's answer, not the player's own name.");
      _ = builder.AppendLine("IMPORTANT: The \"Name\" category must contain a real personal name or a widely recognized fictional or mythical name. Single letters, abbreviations, or nonsense strings are invalid.");
      _ = builder.AppendLine("NEVER copy or compare the player's name field with the answer for the \"Name\" category.");

      // Multi-word and adjective handling
      _ = builder.AppendLine("IMPORTANT: For multi-word answers (e.g. \"Chocolate cake\", \"African lion\"), only the main noun counts.");
      _ = builder.AppendLine(" – If the first word is an adjective (e.g. African, Brazilian, White), the answer is invalid; the noun itself must start with the given letter.");
      _ = builder.AppendLine(" – Example: \"African lion\" is invalid for A (lion begins with L), \"Brazilian monkey\" is invalid for B (monkey begins with M), \"White rhinoceros\" is invalid for W (rhinoceros begins with R).");
      _ = builder.AppendLine(" – Multi-word foods must start with the letter using their main word. \"Chocolate cake\" is valid for C because both words start with C. \"Chocolate mousse\" is NOT valid for M; only the first significant word is considered.");
      _ = builder.AppendLine(" – Always strip accents and lower-case when checking the first letter.");

      // Output format
      _ = builder.AppendLine();
      _ = builder.AppendLine("### Output format:");
      _ = builder.AppendLine("Respond only with valid JSON — no markdown, no commentary, no explanations.");
      _ = builder.AppendLine("Each player must include both 'answers' and 'scores'.");
      _ = builder.AppendLine();
      _ = builder.AppendLine("{");
      _ = builder.AppendLine("  \"letter\": \"S\",");
      _ = builder.AppendLine("  \"categories\": [...],");
      _ = builder.AppendLine("  \"players\": [");
      _ = builder.AppendLine("    {");
      _ = builder.AppendLine("      \"name\": string,");
      _ = builder.AppendLine("      \"answers\": { \"<Category>\": \"<Answer>\", ... },");
      _ = builder.AppendLine("      \"scores\": { \"<Category>\": { \"points\": number, \"reason\": string }, ... }");
      _ = builder.AppendLine("    }");
      _ = builder.AppendLine("  ]");
      _ = builder.AppendLine("}");

      // Input data
      _ = builder.AppendLine();
      _ = builder.AppendLine("### Input data:");
      _ = builder.AppendLine($"Letter: {request.Letter}");
      _ = builder.AppendLine($"Categories: {string.Join(", ", request.Categories)}");
      _ = builder.AppendLine("Players and their answers:");
      foreach (PlayerEntry player in request.Players)
      {
        _ = builder.AppendLine($"- {player.Name}: {JsonSerializer.Serialize(player.Answers)}");
      }

      _ = builder.AppendLine();

      // Final validation phase
      _ = builder.AppendLine("Return ONLY valid JSON following the exact structure above, including:");
      _ = builder.AppendLine("1. 'letter' at the top");
      _ = builder.AppendLine("2. 'categories' array with all categories");
      _ = builder.AppendLine("3. 'players' array, where each player includes 'answers', 'scores'.");
      _ = builder.AppendLine();
      _ = builder.AppendLine("FINAL VALIDATION PHASE (MANDATORY):");
      _ = builder.AppendLine("After constructing the entire JSON output, perform a full second-pass validation over every player's answers:");
      _ = builder.AppendLine(" • Normalize each answer (lowercase, strip accents). For multi-word answers, remove leading adjectives; use the first significant noun.");
      _ = builder.AppendLine($" • Check if the significant word starts with the required letter '{request.Letter}'. If it doesn’t, overwrite the score with {{ \"points\": 0, \"reason\": \"wrong-letter\" }}.");
      _ = builder.AppendLine(" • Check if the answer is a real word in any widely used language and fits the category meaning. Reject nonsense strings, abbreviations, or adjectives used in place of nouns.");
      _ = builder.AppendLine(" • If the answer is empty or missing, set {{ \"points\": 0, \"reason\": \"missing\" }}.");
      _ = builder.AppendLine(" • If the answer passes all checks, set {{ \"points\": 10, \"reason\": \"valid\" }}.");
      _ = builder.AppendLine("Overwrite the original points and reasons from the first pass. This validation is mandatory and must not be skipped.");
      return builder.ToString();
    }

    // ----------------------------------------------------------
    // SEND PROMPT TO OLLAMA (with adaptive prediction + retry)
    // ----------------------------------------------------------
    public async Task<string> SendToModelAsync(string prompt, StopGameRequest? context = null)
    {
      Stopwatch timer = Stopwatch.StartNew();
      int estimatedPromptTokens = EstimateTokenCount(prompt);

      // Compute dynamic prediction limit if context available
      int numPredict = context != null ? EstimateNumPredict(context) : (int?)modelOptions?.GetType().GetProperty("num_predict")?.GetValue(modelOptions) ?? 2048;
      int attempt = 0;
      const int MaxAttempts = 3;

      while (attempt < MaxAttempts)
      {
        attempt++;
        try
        {
          var payload = new
          {
            model = ollamaModel,
            prompt,
            stream = false,
            format = "json",
            options = new
            {
              ((dynamic)modelOptions).temperature,
              num_predict = numPredict
            }
          };

          using StringContent content = new(
              JsonSerializer.Serialize(payload, jsonOpts),
              Encoding.UTF8,
              "application/json");

          using CancellationTokenSource cts = new(ollamaRequestTimeout);

          Log.Information(
              "Ollama request attempt {Attempt} | Model '{Model}' | num_predict={NumPredict} | Timeout={Timeout}s",
              attempt, ollamaModel, numPredict, ollamaRequestTimeout.TotalSeconds);

          using HttpResponseMessage response = await httpClient.PostAsync(ollamaBaseUrl, content, cts.Token);
          string responseText = await response.Content.ReadAsStringAsync();
          timer.Stop();

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

          // Validate JSON integrity
          if (IsValidJson(responseText))
          {
            Log.Information("Ollama response validated successfully on attempt {Attempt}.", attempt);
            return ExtractInnerResponse(responseText);
          }

          Log.Warning("Ollama returned invalid or truncated JSON on attempt {Attempt}. Retrying...", attempt);
          numPredict = Math.Min((int)(numPredict * 1.25), 4096);
        }
        catch (TaskCanceledException ex)
        {
          timer.Stop();
          Log.Warning(ex, "Ollama call exceeded manual timeout ({Timeout}s) after {Elapsed} ms.",
              ollamaRequestTimeout.TotalSeconds, timer.ElapsedMilliseconds);
        }
        catch (JsonException jex)
        {
          timer.Stop();
          Log.Warning(jex, "Malformed JSON detected from Ollama on attempt {Attempt}. Retrying with higher num_predict...", attempt);
          numPredict = Math.Min((int)(numPredict * 1.25), 4096);
        }
        catch (Exception ex)
        {
          timer.Stop();
          Log.Error(ex, "Unexpected error while calling Ollama (attempt {Attempt})", attempt);
        }
      }

      Log.Error("Ollama failed to return valid structured output after {Attempts} attempts.", MaxAttempts);
      return string.Empty;
    }

    private static int EstimateTokenCount(string text)
    {
      return Math.Max(1, text.Length / 4);
    }

    private static bool IsValidJson(string input)
    {
      if (string.IsNullOrWhiteSpace(input))
        return false;

      input = input.Trim();
      if (!(input.StartsWith("{") && input.EndsWith("}")) &&
          !(input.StartsWith("[") && input.EndsWith("]")))
        return false;

      try
      {
        using JsonDocument doc = JsonDocument.Parse(input);
        return doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
      }
      catch
      {
        return false;
      }
    }

    private static string ExtractInnerResponse(string responseText)
    {
      try
      {
        using JsonDocument doc = JsonDocument.Parse(responseText);
        if (doc.RootElement.TryGetProperty("response", out JsonElement inner))
        {
          string result = inner.GetString() ?? string.Empty;
          int firstBrace = result.IndexOf('{');
          return firstBrace >= 0 ? result[firstBrace..].Trim() : result.Trim();
        }
      }
      catch
      {
        // If already valid JSON, return as-is
        return responseText.Trim();
      }

      return responseText.Trim();
    }

    private static string RepairTruncatedJson(string raw)
    {
      int firstBrace = raw.IndexOf('{');
      int lastBrace = raw.LastIndexOf('}');
      if (firstBrace >= 0 && lastBrace > firstBrace)
        return raw.Substring(firstBrace, lastBrace - firstBrace + 1);
      return raw;
    }

    private static int EstimateNumPredict(StopGameRequest request)
    {
      const int BaseTokens = 600; // JSON overhead + prompt + structure
      const int TokensPerCategory = 60; // per category *per player* heuristic
      const double SafetyMargin = 1.5;  // add 50% buffer

      int playerCount = request.Players?.Count ?? 0;
      int categoryCount = request.Categories?.Count ?? 0;

      // Estimate total tokens needed for the output
      int estimated = BaseTokens + (playerCount * categoryCount * TokensPerCategory);
      int adjusted = (int)(estimated * SafetyMargin);

      // Cap between reasonable limits to avoid overspending compute
      return Math.Clamp(adjusted, 512, 6144);
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

      // Helper local function for diacritic removal
      static string RemoveDiacritics(string text)
      {
        if (string.IsNullOrEmpty(text))
        {
          return string.Empty;
        }

        string normalized = text.Normalize(NormalizationForm.FormD);
        return new string([.. normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
      }

      // Normalize a string for accent-insensitive comparison
      static string Normalize(string input)
      {
        return RemoveDiacritics(input).Trim().ToLowerInvariant();
      }

      // Pre-collect all normalized answers per category (accent-insensitive)
      Dictionary<string, List<string>> allAnswers = new();
      foreach (string category in original.Categories)
      {
        allAnswers[category] = [.. original.Players
            .Select(p => p.Answers.TryGetValue(category, out string? a) ? a?.Trim() ?? string.Empty : string.Empty)
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(Normalize)];
      }

      // Validate each player and category
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

          // Structural checks
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
          else
          {
            // Accent-insensitive first-letter validation
            string normalizedAnswer = RemoveDiacritics(answer).ToUpperInvariant();
            string normalizedLetter = RemoveDiacritics(letter).ToUpperInvariant();

            if (!normalizedAnswer.StartsWith(normalizedLetter, StringComparison.Ordinal))
            {
              score.Points = 0;
              score.Reason = $"Does not start with the letter '{letter}'";
            }
            else
            {
              // Only apply uniqueness if AI gave a positive score
              if (score.Points > 0)
              {
                int sameWordCount = allAnswers[category]
                    .Count(a => a.Equals(Normalize(answer), StringComparison.OrdinalIgnoreCase));

                if (sameWordCount > 1)
                {
                  score.Points = 5;
                  score.Reason = "Valid word but duplicated by another player";
                }
                else
                {
                  score.Points = 10;
                  score.Reason = "Valid and unique word";
                }
              }
            }
          }

          player.Scores[category] = score;
        }

        player.Total = player.Scores.Values.Sum(s => s.Points);
      }
    }

    private static string FindJsonFile()
    {
      const string fileName = "sample-response.json";
      string relativePath = Path.Combine("src", "Core", "Features", "Scoring", fileName);

      // Try current directory
      string currentDir = Directory.GetCurrentDirectory();
      string jsonPath = Path.Combine(currentDir, relativePath);
      if (File.Exists(jsonPath))
      {
        return jsonPath;
      }

      // Try from project root (look for WordRush.sln)
      string? searchDir = currentDir;
      while (searchDir != null)
      {
        string slnPath = Path.Combine(searchDir, "WordRush.sln");
        if (File.Exists(slnPath))
        {
          jsonPath = Path.Combine(searchDir, relativePath);
          if (File.Exists(jsonPath))
          {
            return jsonPath;
          }
          break;
        }
        searchDir = Directory.GetParent(searchDir)?.FullName;
      }

      // Try from assembly location
      string? assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
      if (!string.IsNullOrEmpty(assemblyLocation))
      {
        string? assemblyDir = Path.GetDirectoryName(assemblyLocation);
        if (assemblyDir != null)
        {
          // Navigate up from bin/Debug/net9.0 to project root
          for (int i = 0; i < 5 && assemblyDir != null; i++)
          {
            string testPath = Path.Combine(assemblyDir, relativePath);
            if (File.Exists(testPath))
            {
              return testPath;
            }
            assemblyDir = Directory.GetParent(assemblyDir)?.FullName;
          }
        }
      }

      // Return the expected path even if it doesn't exist (for error message)
      return Path.Combine(currentDir, relativePath);
    }
  }
}
