using System.Collections.Generic;

namespace WordRush.Core.Features.StopGame
{
  public class StopGameRequest
  {
    public string Letter { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<PlayerEntry> Players { get; set; } = new();
  }

  public class PlayerEntry
  {
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Answers { get; set; } = new();
  }

  public class StopGameResponse
  {
    public string Letter { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<PlayerResult> Players { get; set; } = new();
  }

  public class PlayerResult
  {
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Answers { get; set; } = new();
    public Dictionary<string, CategoryScore> Scores { get; set; } = new();
    public int Total { get; set; }
  }

  public class CategoryScore
  {
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
  }
}
