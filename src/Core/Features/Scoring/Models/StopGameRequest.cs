using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Scoring.Models
{
  public class StopGameRequest
  {
    public string Letter { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<PlayerEntry> Players { get; set; } = new();
  }
}
