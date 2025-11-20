using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Serilog;
namespace WordRush.Core.Features.Hints
{
  /// <summary>
  /// HintService sends a prompt to an AI model via HTTP and returns a helpful hint.
  /// This implementation uses the same Ollama model endpoint as the scoring service.
  /// </summary>
  public class HintService : IHintService
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

    public HintService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
      httpClient = httpClientFactory.CreateClient();
      // Do not cancel automatically on long runs; we'll handle our own cancellation
      httpClient.Timeout = Timeout.InfiniteTimeSpan;

      // Configure model parameters from appsettings or use defaults
      ollamaModel = config["Ollama:Model"] ?? "llama3";
      ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434/api/generate";

      int timeoutSeconds = int.TryParse(config["Ollama:RequestTimeoutSeconds"], out int t) ? t : 300;
      ollamaRequestTimeout = TimeSpan.FromSeconds(timeoutSeconds);

      modelOptions = new
      {
        temperature = double.TryParse(config["Ollama:Options:temperature"], out double temp) ? temp : 0.15,
        // Keep number of predictions small for hint generation
        num_predict = int.TryParse(config["Ollama:Options:num_predict"], out int np) ? np : 128
      };
    }

    public async Task<string?> GetHintAsync(string letter, string category)
    {
      if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(letter))
        return null;

      string prompt =
          $"Give a single short hint (max 20 words) for a word that belongs to the category '{category}' " +
          $"and starts with the letter '{letter}'. Do not reveal the word itself or mention the letter/category. " +
          $"Be descriptive and specific. Example: Animal/C → 'It’s a common house pet that meows.'";

      var body = new
      {
        model = ollamaModel,
        prompt = prompt,
        stream = false,  // disable stream temporarily for full JSON body
        options = modelOptions
      };

      try
      {
        using CancellationTokenSource cts = new(ollamaRequestTimeout);
        using StringContent content = new(
            JsonSerializer.Serialize(body, jsonOpts),
            Encoding.UTF8,
            "application/json");

        Log.Information("[HINT] Requesting hint from model '{Model}' for {Category}/{Letter}", ollamaModel, category, letter);

        using HttpResponseMessage response = await httpClient.PostAsync(ollamaBaseUrl, content, cts.Token);
        string raw = await response.Content.ReadAsStringAsync(cts.Token);
        Log.Information("[HINT RAW RESPONSE] => {Raw}", raw);

        if (!response.IsSuccessStatusCode)
        {
          Log.Warning("[HINT] Failed: {Status}", response.StatusCode);
          return "No hint available.";
        }

        // Try JSON parsing
        string hint = "";
        try
        {
          using JsonDocument doc = JsonDocument.Parse(raw);
          if (doc.RootElement.TryGetProperty("response", out JsonElement resp))
          {
            hint = resp.GetString() ?? "";
          }
        }
        catch (Exception jex)
        {
          Log.Warning(jex, "[HINT] JSON parse failed, using raw string fallback");
          hint = raw;
        }

        if (string.IsNullOrWhiteSpace(hint))
        {
          Log.Warning("[HINT] Empty hint, raw response was: {Raw}", raw);
          return "No hint available.";
        }

        hint = Regex.Replace(hint, @"(?i)(here.?s your hint:?|can you guess.*?$)", string.Empty, RegexOptions.Singleline).Trim();
        hint = hint.Trim().Trim('"');
        int end = hint.IndexOf('.') + 1;
        if (end > 0 && end < hint.Length)
          hint = hint[..end].Trim();

        Log.Information("[HINT FINAL CLEANED] => {Hint}", hint);
        return hint;
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[HINT] Error fetching hint for {Category}/{Letter}", category, letter);
        return "No hint available (error).";
      }
    }


  }
}
