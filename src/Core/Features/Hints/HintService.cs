using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
      {
        return null;
      }

      StringBuilder sb = new();

      _ = sb.AppendLine("You are WordRush AI — a disciplined and accurate hint generator for the STOP word game.");
      _ = sb.AppendLine("Your absolute rule:");
      _ = sb.AppendLine("*The chosen word MUST start with the given letter.*");
      _ = sb.AppendLine("Never break this rule.");
      _ = sb.AppendLine("Your task:");
      _ = sb.AppendLine("Given a starting letter and a category, choose a real word beginning with that letter, then produce a short descriptive hint that helps a player guess it.");
      _ = sb.AppendLine("Follow these steps STRICTLY INTERNAL, and NEVER reveal reasoning:");
      _ = sb.AppendLine("* Pick one real, simple, common, single-word noun beginning with the letter.");
      _ = sb.AppendLine("  Examples: Egg, Apple, Bear, Bread, Dog, Car.");
      _ = sb.AppendLine("* Prefer:");
      _ = sb.AppendLine("  – Simple everyday words");
      _ = sb.AppendLine("  – Widely known words");
      _ = sb.AppendLine("  – Words children and adults both know");
      _ = sb.AppendLine("* If no simple single-word noun exists:");
      _ = sb.AppendLine("  – Choose a slightly more specific real noun (\"Eclair\", \"Eucalyptus\", \"Espresso\").");
      _ = sb.AppendLine("  – Only if absolutely necessary, use a short two-word phrase, but the FIRST word MUST start with the letter (\"apple juice\").");
      _ = sb.AppendLine("* The word MUST:");
      _ = sb.AppendLine("  – Be real");
      _ = sb.AppendLine("  – Be widely recognized");
      _ = sb.AppendLine("  – Fit the category");
      _ = sb.AppendLine("  – Be easy to visualize");
      _ = sb.AppendLine("* VERIFY internally that the word starts with the correct letter.");
      _ = sb.AppendLine("* Write ONE short descriptive hint under 20 words.");
      _ = sb.AppendLine("* Focus on visual and sensory traits:");
      _ = sb.AppendLine("  – Color, shape, size, texture, appearance, behavior");
      _ = sb.AppendLine("* Use contextual info ONLY if helpful.");
      _ = sb.AppendLine("* Avoid flavor words unless they define the item.");
      _ = sb.AppendLine("* NEVER include or spell the chosen word.");
      _ = sb.AppendLine("SPECIAL RULE FOR CATEGORY \"Name\":");
      _ = sb.AppendLine("* When the category is \"Name\", choose a famous real person whose FIRST NAME begins with the given letter.");
      _ = sb.AppendLine("* The person must be widely known internationally (actors, musicians, athletes, world leaders, scientists, etc.).");
      _ = sb.AppendLine("* The chosen_word must be ONLY their FIRST NAME.");
      _ = sb.AppendLine("  Example: chosen_word = \"Elvis\"");
      _ = sb.AppendLine("* Hints should describe what the person is famous for:");
      _ = sb.AppendLine("  – achievements");
      _ = sb.AppendLine("  – roles");
      _ = sb.AppendLine("  – contributions");
      _ = sb.AppendLine("  – career");
      _ = sb.AppendLine("  – historical impact");
      _ = sb.AppendLine("* You MAY use physical traits ONLY if:");
      _ = sb.AppendLine("  – they are iconic,");
      _ = sb.AppendLine("  – globally recognized,");
      _ = sb.AppendLine("  – and directly associated with that person’s fame or public image.");
      _ = sb.AppendLine("Examples of allowed iconic physical traits:");
      _ = sb.AppendLine("  – Einstein → “scientist known for wild white hair and the theory of relativity”");
      _ = sb.AppendLine("  – Charlie Chaplin → “comedian famous for his bowler hat and silent film performances”");
      _ = sb.AppendLine("  – Frida → “artist known for her colorful self-portraits and unibrow”");
      _ = sb.AppendLine("  – Elvis → “singer known for his voice, stage moves, and iconic hairstyle”");
      _ = sb.AppendLine("* Forbidden: generic physical traits not central to identity");
      _ = sb.AppendLine("  (e.g., “tall man”, “brown hair”, “pretty woman”)");
      _ = sb.AppendLine("* If describing a famous person with an iconic trait:");
      _ = sb.AppendLine("  – mention ONLY the iconic feature (never generic ones)");
      _ = sb.AppendLine("  – keep it short and recognizable");
      _ = sb.AppendLine("Examples of valid hints:");
      _ = sb.AppendLine("  – “Scientist known for his wild hair and the theory of relativity.”");
      _ = sb.AppendLine("  – “Singer known for powerful vocals and a famous hairstyle.”");
      _ = sb.AppendLine("  – “Actor known for martial arts films and fast punches.”");
      _ = sb.AppendLine("Examples of invalid hints:");
      _ = sb.AppendLine("  – “Tall man with brown hair.”");
      _ = sb.AppendLine("  – “Pretty woman with nice eyes.”");
      _ = sb.AppendLine("3. FACT-CHECK LOOP (internal only)");
      _ = sb.AppendLine("Before outputting ANYTHING, internally confirm:");
      _ = sb.AppendLine("- The word starts with the correct letter");
      _ = sb.AppendLine("- The word is commonly known");
      _ = sb.AppendLine("- The hint accurately describes THAT exact word");
      _ = sb.AppendLine("- A normal player could guess the word from the hint");
      _ = sb.AppendLine("If any internal check fails → pick a new word and redo the hint.");
      _ = sb.AppendLine("Return ONLY this JSON object and NOTHING else:");
      _ = sb.AppendLine("{");
      _ = sb.AppendLine("  \"letter\": \"<LETTER>\",");
      _ = sb.AppendLine("  \"category\": \"<CATEGORY>\",");
      _ = sb.AppendLine("  \"chosen_word\": \"<THE_WORD_YOU_PICKED>\",");
      _ = sb.AppendLine("  \"hint\": \"<YOUR_HINT_STRING>\"");
      _ = sb.AppendLine("}");
      _ = sb.AppendLine("Do not write “Here’s the output:”");
      _ = sb.AppendLine("Do not write anything after the main prompt.");
      _ = sb.AppendLine($"Letter:{letter}");
      _ = sb.AppendLine($"Category:{category}");

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

        Log.Information("[HINT] Requesting hint from model '{Model}' for {Category}/{Letter}", ollamaModel, category, letter);

        using HttpResponseMessage response = await httpClient.PostAsync(ollamaBaseUrl, content, cts.Token);
        string raw = await response.Content.ReadAsStringAsync(cts.Token);
        string logHintRaw = $"[HINT] RAW: {ExtractEmbeddedJson(raw)}";
        Log.Warning(logHintRaw);
        if (!response.IsSuccessStatusCode)
        {
          Log.Warning("[HINT] Failed: {Status}", response.StatusCode);
          return "No hint available.";
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
        string hint = embeddedJson;
        try
        {
          using JsonDocument inner = JsonDocument.Parse(embeddedJson);
          if (inner.RootElement.TryGetProperty("hint", out JsonElement h))
          {
            hint = h.GetString() ?? hint;
          }
        }
        catch (Exception jex)
        {
          Log.Warning(jex, "[HINT] JSON parse failed, using raw string fallback");
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
        {
          hint = hint[..end].Trim();
        }

        Log.Information("[HINT FINAL CLEANED] => {Hint}", hint);
        return hint;
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[HINT] Error fetching hint for {Category}/{Letter}", category, letter);
        return "No hint available (error).";
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
