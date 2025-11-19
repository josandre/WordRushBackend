using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Scoring.Models
{
  public class StopGameResponse
  {
    public string Letter { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<PlayerResult> Players { get; set; } = new();
    public string? WinnerName { get; set; }
    public int? WinnerUserId { get; set; }
  }
}
