namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  [Serializable]
  public class GameAnswer
  {
    // TODO: Replace this with an actual category
    public string Category { get; set; } = string.Empty; // The category that this answer belongs

    public string Answer { get; set; } = string.Empty; // The text used as the answer for the category

    public GameAnswerResult Result { get; set; } = new(); // Reference to the result of this answer

    public GameAnswer(string category, string answer)
    {
      Category = category;
      Answer = answer;

      Result = new();
    }
  }
}
