namespace WordRush.Core.Features.Realtime.Models;

public class Round
{
  public Dictionary<string, List<string>> PlayerResponses { get; set; } = new();
}
