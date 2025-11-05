using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Scoring.Models
{
  public class CategoryScore
  {
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
  }
}
