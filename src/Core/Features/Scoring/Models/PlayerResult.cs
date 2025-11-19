using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Scoring.Models
{
  public class PlayerResult
  {
    public string Name { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public Dictionary<string, string> Answers { get; set; } = new();
    public Dictionary<string, CategoryScore> Scores { get; set; } = new();
    public int Total { get; set; }
  }
}
