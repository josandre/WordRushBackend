using WordRush.Core.Features.Scoring.Models;

namespace WordRush.Core.Features.Scoring
{
  /// <summary>
  /// Defines the contract for scoring services used in WordRush.
  /// Responsible for evaluating game rounds, building prompts,
  /// sending them to AI models, and returning structured results.
  /// </summary>
  public interface IScoringService
  {
    /// <summary>
    /// Builds the text prompt sent to the AI model for scoring a round.
    /// </summary>
    /// <param name="request">The input game data (letter, categories, and answers).</param>
    /// <returns>A formatted text prompt ready for AI evaluation.</returns>
    string BuildPrompt(StopGameRequest request);

    /// <summary>
    /// Sends the formatted prompt to the AI model and retrieves the raw JSON response.
    /// </summary>
    /// <param name="prompt">The AI prompt to evaluate.</param>
    /// <returns>The raw JSON response returned by the model.</returns>
    Task<string> SendToModelAsync(string prompt, StopGameRequest? context);

    /// <summary>
    /// Parses the AI response into a structured <see cref="StopGameResponse"/>.
    /// </summary>
    /// <param name="modelResponse">The raw JSON text from the AI model.</param>
    /// <returns>The deserialized <see cref="StopGameResponse"/>.</returns>
    StopGameResponse? ParseResponse(string responseText, StopGameRequest originalRequest);

    /// <summary>
    /// Evaluates a full round by building the prompt, sending it to the model,
    /// and returning a parsed, scored result.
    /// </summary>
    /// <param name="request">The input data for the round.</param>
    /// <returns>The scored result.</returns>
    Task<StopGameResponse?> ScoreGameAsync(StopGameRequest request);
  }
}
