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
        var answers = player.Answers.Values
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
      _ = sb.AppendLine("You are an expert lexical and semantic judge for the word game 'STOP!'.");
      _ = sb.AppendLine("Your task is to evaluate each player's answers for validity, semantics, and correctness according to the given categories and the provided starting letter.");
      _ = sb.AppendLine("You must return ONLY valid, well-structured JSON with the following exact format:");
      _ = sb.AppendLine("{\"letter\":\"C\",\"categories\":[\"Name\",\"Country or City\",\"Animal\",\"Fruit or Food\",\"Color\"],\"players\":[{\"name\":\"Alice\",\"answers\":{\"Name\":\"Carlos\",\"Country or City\":\"Chile\",\"Animal\":\"Caballo\",\"Fruit or Food\":\"Chocolate\",\"Color\":\"Celeste\"},\"scores\":{\"Name\":{\"points\":10,\"reason\":\"Valid word for category\"},\"Country or City\":{\"points\":10,\"reason\":\"Valid country or city\"},\"Animal\":{\"points\":10,\"reason\":\"Valid animal\"},\"Fruit or Food\":{\"points\":10,\"reason\":\"Valid fruit or food\"},\"Color\":{\"points\":10,\"reason\":\"Valid color\"}}},{\"name\":\"Bob\",\"answers\":{\"Name\":\"Carmen\",\"Country or City\":\"Canada\",\"Animal\":\"Camaleón\",\"Fruit or Food\":\"Cereal\",\"Color\":\"Café\"},\"scores\":{\"Name\":{\"points\":10,\"reason\":\"Valid word for category\"},\"Country or City\":{\"points\":10,\"reason\":\"Valid country or city\"},\"Animal\":{\"points\":10,\"reason\":\"Valid animal\"},\"Fruit or Food\":{\"points\":10,\"reason\":\"Valid fruit or food\"},\"Color\":{\"points\":10,\"reason\":\"Valid color\"}}}]}\r\n");
      //_ = sb.AppendLine("{");
      //_ = sb.AppendLine("  \"letter\": \"A\",");
      //_ = sb.AppendLine("  \"categories\": [\"Name\", \"Country or City\", \"Animal\", \"Fruit or Food\", \"Color\"],");
      //_ = sb.AppendLine("  \"players\": [");
      //_ = sb.AppendLine("    {");
      //_ = sb.AppendLine("      \"name\": \"PlayerName\",");
      //_ = sb.AppendLine("      \"answers\": {");
      //_ = sb.AppendLine("        \"Name\": \"word\",");
      //_ = sb.AppendLine("        \"Country or City\": \"word\",");
      //_ = sb.AppendLine("        \"Animal\": \"word\",");
      //_ = sb.AppendLine("        \"Fruit or Food\": \"word\",");
      //_ = sb.AppendLine("        \"Color\": \"word\"");
      //_ = sb.AppendLine("      },");
      //_ = sb.AppendLine("      \"scores\": {");
      //_ = sb.AppendLine("        \"Name\": { \"points\": 10, \"reason\": \"Valid word for category\" },");
      //_ = sb.AppendLine("        \"Country or City\": { \"points\": 10, \"reason\": \"Valid country or city\" },");
      //_ = sb.AppendLine("        \"Animal\": { \"points\": 10, \"reason\": \"Valid animal\" },");
      //_ = sb.AppendLine("        \"Fruit or Food\": { \"points\": 10, \"reason\": \"Valid fruit or food\" },");
      //_ = sb.AppendLine("        \"Color\": { \"points\": 10, \"reason\": \"Valid color\" }");
      //_ = sb.AppendLine("      }");
      //_ = sb.AppendLine("    }");
      //_ = sb.AppendLine("  ]");
      //_ = sb.AppendLine("}");

      // ===== SCORING INSTRUCTIONS =====
      _ = sb.AppendLine("1. The letter defines the starting character that every valid word must begin with.");
      _ = sb.AppendLine("1a. Ignore capitalization and accents when matching the first letter. For example, 'Árbol' is valid for letter 'A'.");
      _ = sb.AppendLine("1b. When checking if a word starts with the given letter, ignore accents and case entirely. For example, 'Águila' counts as starting with 'A', and 'árbol' counts as 'A'.");
      _ = sb.AppendLine("1c. Never penalize a word for accented first letters — treat 'Á', 'É', 'Í', 'Ó', 'Ú', 'Ü' as their plain equivalents.");
      _ = sb.AppendLine("2. Evaluate each answer in its category and assign points with reasoning.");
      _ = sb.AppendLine("3. Output must always be strictly valid JSON and use only ASCII quotes, no trailing commas.");
      _ = sb.AppendLine("4. Do not omit any player, category, or score fields. Never invent new keys.");
      _ = sb.AppendLine("5. Give one of three point values per answer:");
      _ = sb.AppendLine("   - 10 = Valid and fits category meaning.");
      _ = sb.AppendLine("   - 5  = Valid but duplicated by another player in the same category.");
      _ = sb.AppendLine("   - 0  = Invalid, nonsense, or does not fit the category meaning.");

      // ===== VALIDATION LOGIC =====
      _ = sb.AppendLine("1. A valid answer must be a real or commonly recognized word beginning with the given letter.");
      _ = sb.AppendLine("2. Reject nonsense, random characters, or strings with no lexical meaning.");
      _ = sb.AppendLine("3. If a player leaves an answer blank, assign 0 with reason 'Missing answer'.");
      _ = sb.AppendLine("4. Reject words that don't conceptually fit the category (e.g., 'Banana' for Animal).");
      _ = sb.AppendLine("5. Accept plural and singular forms equally ('cats' == 'cat').");
      _ = sb.AppendLine("6. Be culturally aware of Spanish and English words — both languages are allowed.");
      _ = sb.AppendLine("7. Treat singular and plural forms as equivalent for duplication (e.g. 'Aguila' and 'Águila' are the same).");
      _ = sb.AppendLine("8. Foods like 'Arroz', 'Pasta', 'Pan', 'Leche', 'Huevos', etc. are valid even if they are ingredients, not dishes.");
      _ = sb.AppendLine("9. When comparing answers for duplicates, only mark them as duplicates if they are exactly the same normalized word (ignore case and accents).");

      // ===== ACCENT & DIACRITIC NORMALIZATION =====
      _ = sb.AppendLine("Treat accented letters (á, é, í, ó, ú, ü, ñ, ç) as their unaccented equivalents for validation and comparison.");
      _ = sb.AppendLine("Example: 'Canadá' = 'Canada', 'Camaleón' = 'Camaleon', 'Brócoli' = 'Brocoli', 'Bogotá' = 'Bogota'.");
      _ = sb.AppendLine("Never mark a valid Spanish or accented word as invalid solely because of its accent or case.");
      _ = sb.AppendLine("Always accept equivalent Spanish and English forms of the same country or city (e.g., 'Canadá' = 'Canada', 'Atenas' = 'Athens').");
      _ = sb.AppendLine("If two forms differ only by accent or language, treat them as the same word for validation and duplication purposes.");
      _ = sb.AppendLine("Ensure that accented first letters are treated identically to their unaccented counterparts when validating the starting letter.");

      // ===== NAME CATEGORY RULES =====
      _ = sb.AppendLine("1. Accept real given names from any culture (e.g., 'Ana', 'Carlos', 'Bo', 'Aby').");
      _ = sb.AppendLine("2. Accept well-known fictional or mythological names (e.g., 'Cersei', 'Gandalf', 'Lara', 'Mario').");
      _ = sb.AppendLine("3. Reject strings that are clearly nonsense or random (e.g., 'Aaaz', 'Bbb', 'Asd').");
      _ = sb.AppendLine("4. Two-letter names are valid only if they correspond to known names (e.g., 'Ed', 'Bo', 'Al').");
      _ = sb.AppendLine("5. Assign 0 points if the word does not plausibly represent a person's name.");

      // ===== COUNTRY OR CITY CATEGORY =====
      _ = sb.AppendLine("1. Accept any real country, city, state, or region name as valid. Do not require it to be a country only.");
      _ = sb.AppendLine("2. Recognize both English and Spanish spellings (e.g., 'México', 'Mexico', 'Bogotá', 'Bogota').");
      _ = sb.AppendLine("3. Reject fictional or non-geographic words.");
      _ = sb.AppendLine("4. Always treat accented and unaccented versions of the same location as equivalent ('Canadá' = 'Canada', 'Atenas' = 'Athens').");
      _ = sb.AppendLine("5. Accept any real city, state, or region as valid — do not reject just because it is not a country.");
      _ = sb.AppendLine("   Examples: 'Cali', 'Paris', 'New York', 'Barcelona' are valid.");
      _ = sb.AppendLine("6. Accept any real city or region, not only countries.");


      // ===== ANIMAL CATEGORY =====
      _ = sb.AppendLine("1. Accept any real animal species (singular form preferred).");
      _ = sb.AppendLine("2. Ignore accents ('Camaleón' == 'Camaleon').");
      _ = sb.AppendLine("3. Reject objects, foods, or mythological creatures unless clearly animal-like ('Dragon' may be accepted if treated as an animal).");
      _ = sb.AppendLine("4. Do not reject common animals such as 'caballo', 'perro', 'gato', or 'vaca'. These are valid even if generic species names.");

      // ===== FRUIT OR FOOD CATEGORY =====
      _ = sb.AppendLine("1. Accept edible foods, ingredients, or dishes ('Arroz', 'Chocolate Cake').");
      _ = sb.AppendLine("2. Multi-word dishes are valid ('Ice Cream', 'Chocolate Cake').");
      _ = sb.AppendLine("3. Reject non-edible items or non-food nouns.");
      _ = sb.AppendLine("4. Accept foods in either Spanish or English (e.g., 'Arroz' = 'Rice', 'Pan' = 'Bread', 'Chocolate Cake' = 'Pastel de chocolate').");
      _ = sb.AppendLine("5. Accept dishes or meals even if they are generic or multi-word (e.g., 'Carne asada', 'Fried chicken'). Only reject words that are clearly non-edible.");
      _ = sb.AppendLine("6. Accept generic or prepared dishes even if not specific fruits (e.g., 'Carne asada', 'Pasta', 'Soup').");
      _ = sb.AppendLine("5. Consider all edible foods, ingredients, fruits, or dishes as valid, even if generic or multi-word.");
      _ = sb.AppendLine("   Examples: 'Carne asada', 'Chocolate cake', 'Chayote', 'Rice', 'Soup'.");
      _ = sb.AppendLine("   Only reject non-edible items or words unrelated to food.");


      // ===== COLOR CATEGORY =====
      _ = sb.AppendLine("1. Accept any recognized color or shade ('Azul', 'Rojo', 'Beige', 'Café', 'Celeste').");
      _ = sb.AppendLine("2. Reject words that are not color descriptors.");
      _ = sb.AppendLine("3. Accept recognized color names in Spanish, English, or Portuguese (e.g., 'Azul', 'Amarillo', 'Rojo', 'Amarelo', 'Blue', 'Beige').");

      // ===== DUPLICATE LOGIC =====
      _ = sb.AppendLine("If two or more players submit the same valid word for a category, each should receive 5 points with reason 'Valid but duplicated by another player in the same category'.");
      _ = sb.AppendLine("Duplicates must be checked after accent removal, case normalization, and trimming spaces. For example, 'Brócoli' and 'Brocoli' count as duplicates.");
      _ = sb.AppendLine("Do not mark words as duplicates just because they belong to the same category or share the same starting letter.");
      _ = sb.AppendLine("A word is a duplicate only if it is textually the same as another player's answer after normalization (case-insensitive, accent-insensitive, and trimmed).");
      _ = sb.AppendLine("Only mark as duplicated if the normalized lowercase words are identical. Do not mark similar but different words as duplicates.");
      _ = sb.AppendLine("For example: 'Azul' and 'Amarelo' are both valid colors but NOT duplicates.");

      // ===== REASON FIELD SANITIZATION =====
      _ = sb.AppendLine("Each 'reason' must be a short plain sentence using only letters, digits, spaces, and punctuation such as periods or commas.");
      _ = sb.AppendLine("Do NOT include parentheses, quotation marks, or colons inside the reason text.");
      _ = sb.AppendLine("Example: use 'Invalid country or city name' instead of 'Invalid country or city (should be Athens)'.");
      _ = sb.AppendLine("All text values must be enclosed in standard double quotes. Never use single quotes or backticks.");
      _ = sb.AppendLine("Each 'reason' should be clear and objective. Avoid speculative phrases like 'might be', 'perhaps', or question marks."); 

      // ===== OUTPUT CONSTRAINT =====
      _ = sb.AppendLine("Your final response must be strictly valid JSON — no markdown, no explanations, no prose, no comments, no extra keys. Only the structured JSON object exactly as defined.");
      _ = sb.AppendLine("Ensure each 'answers' and 'scores' dictionary has one entry per category.");
      _ = sb.AppendLine("Never include phrases like 'Here is the JSON:' or commentary before or after the object — output must start with '{' and end with '}'."); 
      //_ = sb.AppendLine("Output compact JSON using all lowercase property names.");
      _ = sb.AppendLine(JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));

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

          if (timer.ElapsedMilliseconds > 20000)
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

    private static int EstimateNumPredict(StopGameRequest request)
    {
      const int BaseTokens = 600; // JSON overhead + prompt + structure
      const int TokensPerCategory = 50; // per category *per player* heuristic
      const double SafetyMargin = 1.3;  // add 30% buffer

      int playerCount = request.Players?.Count ?? 0;
      int categoryCount = request.Categories?.Count ?? 0;

      // Estimate total tokens needed for the output
      int estimated = BaseTokens + (playerCount * categoryCount * TokensPerCategory);
      int adjusted = (int)(estimated * SafetyMargin);

      // Cap between reasonable limits to avoid overspending compute
      return Math.Clamp(adjusted, 512, 4096);
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
  }
}
