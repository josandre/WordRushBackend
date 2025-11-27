using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace WordRush.Core.Features.Settings
{
  public class CategoryValidationService : ICategoryValidationService
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

    public CategoryValidationService(IHttpClientFactory httpClientFactory, IConfiguration config)
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

    public async Task<bool?> GetCategoryValidationAsync(string categoryName)
    {
      if (string.IsNullOrWhiteSpace(categoryName))
      {
        return null;
      }

      var sb = new StringBuilder();

      sb.AppendLine("You are the Category Qualification Engine for the WordRush STOP game.");
      sb.AppendLine("Your task is to evaluate whether the provided phrase is a valid STOP category.");
      sb.AppendLine();
      sb.AppendLine("A phrase is a VALID category only if:");
      sb.AppendLine("- It represents a class or type of things (e.g., Animals, Tableware, Famous Person).");
      sb.AppendLine("- Players can provide answers starting with different letters.");
      sb.AppendLine("- It is not vague (e.g., Things, Stuff).");
      sb.AppendLine("- It is not narrow or hyper-specific (a single city, a single object).");
      sb.AppendLine("- It is not a single entity (Eiffel Tower, Michael Jordan).");
      sb.AppendLine("- It is not a question.");
      sb.AppendLine("- It is not an opinion or subjective category (Best movies).");
      sb.AppendLine("- It must be appropriate for all audiences.");
      sb.AppendLine("- Multi-word and multilingual phrases are allowed.");
      sb.AppendLine();
      sb.AppendLine("Evaluate the phrase strictly as VALID or INVALID for STOP gameplay.");
      sb.AppendLine();
      sb.AppendLine("Return ONLY the following JSON with a boolean result:");
      sb.AppendLine("{");
      sb.AppendLine("  \"category\": \"<CATEGORY_PHRASE>\",");
      sb.AppendLine("  \"isValid\": true_or_false");
      sb.AppendLine("}");
      sb.AppendLine();
      sb.AppendLine($"Phrase to evaluate: \"{categoryName}\"");

      string sbPrompt = sb.ToString();

      var body = new
      {
        model = ollamaModel,
        prompt = sbPrompt,
        stream = false,
        options = modelOptions
      };

      try
      {
        using CancellationTokenSource cts = new(ollamaRequestTimeout);
        using StringContent content = new(
            JsonSerializer.Serialize(body, jsonOpts),
            Encoding.UTF8,
            "application/json");

        Log.Information("[CATEGORY VALIDATION] Requesting valid check '{Model}' for {Category}", ollamaModel, categoryName);

        using HttpResponseMessage response = await httpClient.PostAsync(ollamaBaseUrl, content, cts.Token);
        string raw = await response.Content.ReadAsStringAsync(cts.Token);
        string logHintRaw = $"[CATEGORY VALIDATION] RAW: {ExtractEmbeddedJson(raw)}";
        Log.Warning(logHintRaw);
        if (!response.IsSuccessStatusCode)
        {
          Log.Warning("[CATEGORY VALIDATION] Failed: {Status}", response.StatusCode);
          return false;
        }

        string embeddedJson = string.Empty;
        try
        {
          using JsonDocument outer = JsonDocument.Parse(raw);
          if (outer.RootElement.TryGetProperty("response", out JsonElement resp))
          {
            embeddedJson = resp.GetString() ?? string.Empty;
          }
        }
        catch
        {
          embeddedJson = raw;
        }

        // Extract ONLY the "hint" field from the embedded JSON
        string stringValue = embeddedJson;
        bool isValid = false;

        try
        {
          using JsonDocument inner = JsonDocument.Parse(embeddedJson);
          if (inner.RootElement.TryGetProperty("isValid", out JsonElement h))
          {
            isValid = h.GetBoolean();
          }
        }
        catch (Exception jex)
        {
          Log.Warning(jex, "[CATEGORY VALIDATION] JSON parse failed, using raw string fallback");
        }

        if (string.IsNullOrWhiteSpace(stringValue))
        {
          Log.Warning("[CATEGORY VALIDATION] Empty data, raw response was: {Raw}", raw);
          return false;
        }

        return isValid;
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[CATEGORY VALIDATION] Error fetching hint for {Category}", categoryName);
        return false;
      }
    }

    public static string ExtractEmbeddedJson(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
      {
        return "{}";
      }

      try
      {
        using JsonDocument doc = JsonDocument.Parse(raw);

        // If `response` exists → return it directly
        if (doc.RootElement.TryGetProperty("response", out JsonElement resp))
        {
          string inner = resp.GetString();
          if (!string.IsNullOrWhiteSpace(inner))
          {
            return inner;
          }
        }
      }
      catch
      {
        // Ignore. We fall back to heuristics below.
      }

      // Fallback: strip context manually
      try
      {
        int ctxIndex = raw.IndexOf("\"context\"");
        if (ctxIndex > 0)
        {
          // Cut everything from "context" onward
          raw = raw[..ctxIndex];
        }

        // Clean trailing commas and braces
        raw = raw.Trim();
        raw = raw.TrimEnd(',', ' ', '\r', '\n', '\t');
      }
      catch (FormatException ex)
      {
        Log.Error(ex, $"If even fallback fails, return sanitized raw");
      }

      return raw;
    }
  }
}
