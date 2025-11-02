using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WordRush.Core.Features.StopGame;
using Microsoft.Extensions.Configuration;
namespace WordRush.Core.Features.Scoring
{
  public class StopGameScoringService : IScoringService
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<StopGameScoringService> logger;
    private readonly string ollamaModel;
    private readonly string ollamaBaseUrl;
    public StopGameScoringService(IHttpClientFactory httpClientFactory, ILogger<StopGameScoringService> logger, IConfiguration config)
    {
      _httpClient = httpClientFactory.CreateClient();
      this.logger = logger;
      // Read Ollama settings from appsettings.json
      ollamaModel = config["Ollama:Model"] ?? "llama3";
      ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434/api/generate";
    }

    public async Task<StopGameResponse?> ScoreGameAsync(StopGameRequest request)
    {
      string prompt = BuildPrompt(request);
      string modelOutput = await SendToModelAsync(prompt);

      logger.LogInformation($"Ollama raw output (first 200 chars): {modelOutput[..Math.Min(200, modelOutput.Length)]}");

      StopGameResponse? parsed = ParseResponse(modelOutput, request);

      if (parsed == null)
      {
        logger.LogWarning("Ollama returned no structured output. Returning fallback response.");

        StopGameResponse fallback = new StopGameResponse
        {
          Letter = request.Letter,
          Categories = request.Categories,
          Players = new List<PlayerResult>()
        };

        foreach (PlayerEntry player in request.Players)
        {
          PlayerResult result = new PlayerResult
          {
            Name = player.Name,
            Answers = player.Answers,
            Scores = new Dictionary<string, CategoryScore>()
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

      return parsed;
    }

    // ----------------------------------------------------------
    // Prompt Builder
    // ----------------------------------------------------------
    public string BuildPrompt(StopGameRequest request)
    {
      StringBuilder builder = new StringBuilder();

      builder.AppendLine("You are a strict JSON scoring engine for the word game STOP.");
      builder.AppendLine("You receive a list of players, categories, and their answers.");
      builder.AppendLine("Your task is to assign scores to each answer according to the rules, include the original answers, compute the total score per player, and include the categories list in the output.");
      builder.AppendLine();
      builder.AppendLine("Rules:");
      builder.AppendLine("1. Only words that start with the given letter are valid.");
      builder.AppendLine("2. The word must match the category meaning.");
      builder.AppendLine("3. Valid and unique answers = 10 points.");
      builder.AppendLine("4. Valid but duplicated answers = 5 points.");
      builder.AppendLine("5. Invalid, missing, or wrong-letter answers = 0 points.");
      builder.AppendLine("6. Each player must have a 'total' field equal to the sum of all category points.");
      builder.AppendLine("7. The top-level JSON object must include 'letter', 'categories', and 'players'.");
      builder.AppendLine();
      builder.AppendLine("### Output format:");
      builder.AppendLine("Respond only with valid JSON — no markdown, no commentary, no explanations.");
      builder.AppendLine("Each player must include both 'answers', 'scores', and a numeric 'total'.");
      builder.AppendLine();
      builder.AppendLine("Example structure:");
      builder.AppendLine("{");
      builder.AppendLine("  \"letter\": \"S\",");
      builder.AppendLine("  \"categories\": [\"Name\", \"Animal\", \"Food\", \"Country\", \"Color\", \"Object\"],");
      builder.AppendLine("  \"players\": [");
      builder.AppendLine("    {");
      builder.AppendLine("      \"name\": \"Alice\",");
      builder.AppendLine("      \"answers\": {");
      builder.AppendLine("        \"Name\": \"Samuel\",");
      builder.AppendLine("        \"Animal\": \"Snake\",");
      builder.AppendLine("        \"Food\": \"Soup\",");
      builder.AppendLine("        \"Country\": \"Spain\",");
      builder.AppendLine("        \"Color\": \"Silver\",");
      builder.AppendLine("        \"Object\": \"Spoon\"");
      builder.AppendLine("      },");
      builder.AppendLine("      \"scores\": {");
      builder.AppendLine("        \"Name\": { \"points\": 10, \"reason\": \"Valid and unique word\" },");
      builder.AppendLine("        \"Animal\": { \"points\": 5, \"reason\": \"Valid word but duplicated by another player\" },");
      builder.AppendLine("        \"Food\": { \"points\": 10, \"reason\": \"Valid and unique word\" },");
      builder.AppendLine("        \"Country\": { \"points\": 5, \"reason\": \"Valid word but duplicated by another player\" },");
      builder.AppendLine("        \"Color\": { \"points\": 5, \"reason\": \"Valid word but duplicated by another player\" },");
      builder.AppendLine("        \"Object\": { \"points\": 5, \"reason\": \"Valid word but duplicated by another player\" }");
      builder.AppendLine("      },");
      builder.AppendLine("      \"total\": 40");
      builder.AppendLine("    }");
      builder.AppendLine("  ]");
      builder.AppendLine("}");
      builder.AppendLine();
      builder.AppendLine("### Input data:");
      builder.AppendLine($"Letter: {request.Letter}");
      builder.AppendLine($"Categories: {string.Join(", ", request.Categories)}");
      builder.AppendLine("Players and their answers:");

      foreach (PlayerEntry player in request.Players)
      {
        builder.AppendLine($"- {player.Name}: {JsonSerializer.Serialize(player.Answers)}");
      }

      builder.AppendLine();
      builder.AppendLine("Return ONLY valid JSON following the exact structure above, including:");
      builder.AppendLine("1. 'letter' at the top");
      builder.AppendLine("2. 'categories' array with all categories");
      builder.AppendLine("3. 'players' array, where each player includes 'answers', 'scores', and 'total'.");

      return builder.ToString();
    }


    // ----------------------------------------------------------
    // Send Prompt to Ollama (Non-streamed)
    // ----------------------------------------------------------
    public async Task<string> SendToModelAsync(string _prompt)
    {
      try
      {
        var body = new
        {
          model = ollamaModel,
          prompt = _prompt,
          stream = false
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(ollamaBaseUrl, content);

        if (!response.IsSuccessStatusCode)
        {
          string err = await response.Content.ReadAsStringAsync();
          logger.LogError($"Ollama API returned error: {response.StatusCode} - {err}");
          return string.Empty;
        }

        string json = await response.Content.ReadAsStringAsync();

        // Extract inner JSON from Ollama’s response
        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("response", out var inner))
        {
          string innerText = inner.GetString() ?? string.Empty;
          int firstBrace = innerText.IndexOf('{');
          if (firstBrace >= 0)
            innerText = innerText.Substring(firstBrace);
          return innerText.Trim();
        }

        logger.LogWarning("Ollama response did not contain a 'response' field.");
        return string.Empty;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error calling Ollama API");
        return string.Empty;
      }
    }

    // ----------------------------------------------------------
    // Parse Ollama Response
    // ----------------------------------------------------------
    public StopGameResponse? ParseResponse(string responseText, StopGameRequest originalRequest)
    {
      try
      {
        logger.LogInformation("Raw Ollama output: {Text}", responseText);

        if (string.IsNullOrWhiteSpace(responseText))
        {
          logger.LogWarning("Ollama response is empty.");
          return null;
        }

        // Extract the first valid JSON block (in case model adds text before/after)
        int firstBrace = responseText.IndexOf('{');
        int lastBrace = responseText.LastIndexOf('}');
        if (firstBrace == -1 || lastBrace == -1 || lastBrace <= firstBrace)
        {
          logger.LogWarning("No valid JSON braces found in Ollama output.");
          return null;
        }

        string jsonOnly = responseText.Substring(firstBrace, lastBrace - firstBrace + 1);

        StopGameResponse? parsed = JsonSerializer.Deserialize<StopGameResponse>(
            jsonOnly,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (parsed == null)
        {
          logger.LogWarning("Deserialized response is null.");
          return null;
        }

        // ✅ Safeguard 1: if categories are missing or empty, reuse original request categories
        if (parsed.Categories == null || parsed.Categories.Count == 0)
        {
          parsed.Categories = new List<string>(originalRequest.Categories);
          logger.LogInformation("Categories missing from Ollama output — using original request categories.");
        }

        // ✅ Safeguard 2: ensure letter consistency
        if (string.IsNullOrWhiteSpace(parsed.Letter))
        {
          parsed.Letter = originalRequest.Letter;
        }

        // ✅ Safeguard 3: ensure all players have initialized answers, scores, and totals
        foreach (PlayerResult player in parsed.Players)
        {
          if (player.Answers == null)
          {
            player.Answers = new Dictionary<string, string>();
          }

          if (player.Scores == null)
          {
            player.Scores = new Dictionary<string, CategoryScore>();
          }

          // ✅ Sanity Scoring Patch: ensure every category is present in Scores
          foreach (string category in parsed.Categories)
          {
            if (!player.Scores.ContainsKey(category))
            {
              player.Scores[category] = new CategoryScore
              {
                Points = 0,
                Reason = "Missing from model output"
              };
              logger.LogInformation("Patched missing category '{Category}' for player '{Player}'.", category, player.Name);
            }
          }

          // ✅ Ensure total is correct (sum of all category points)
          int computedTotal = player.Scores.Values.Sum(s => s.Points);
          player.Total = computedTotal;
        }

        return parsed;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Failed to parse Ollama response");
        return null;
      }
    }

  }
}
