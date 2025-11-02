using WordRush.Core.Features.Game.Models;

public class GameSettings
{
  private List<string> letters = new() { "A" };

  public int TimeLimit { get; set; } = 45; // seconds

  public LetterOrder Order { get; set; } = LetterOrder.Ascending;

  public string[] Letters
  {
    get => letters.ToArray();
    set
    {
      if (value != null && value.Length > 5)
      {
        letters = value.Take(5).ToList();
      }
      else
      {
        letters = value?.ToList() ?? new List<string>();
      }
    }
  }

}

