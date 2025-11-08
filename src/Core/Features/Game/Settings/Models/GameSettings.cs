using WordRush.Core.Features.Game.Models;

public class GameSettings
{
  public const int MAX_LETTERS = 5;
  public const int MAX_CATEGORIES = 5;

  private List<string> letters = new() { "A", "B", "C" };

  private List<string> categories = new() { "Name" };

  public int TimeLimit { get; set; } = 45; // seconds

  public LetterOrder Order { get; set; } = LetterOrder.Ascending;

  public string[] LettersArray
  {
    get => letters.ToArray();
    set
    {
      if (value != null && value.Length > MAX_LETTERS)
      {
        letters = value.Take(5).ToList();
      }
      else
      {
        letters = value?.ToList() ?? new List<string>();
      }
    }
  }

  public string[] CategoriesArray
  {
    get => categories.ToArray();
    set
    {
      if (value != null && value.Length > MAX_CATEGORIES)
      {
        categories = value.Take(5).ToList();
      }
      else
      {
        categories = value?.ToList() ?? new List<string>();
      }
    }
  }
}

