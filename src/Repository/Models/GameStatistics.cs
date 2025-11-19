using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordRush.Repository.Models;

public class GameStatistics
{
  [Key]
  public int Id { get; set; }

  [Required]
  public int UserId { get; set; }

  [Required]
  public int TotalPlayedGame { get; set; } = 0;

  [Required]
  public int WonGames { get; set; } = 0;

  [Required]
  public int TotalStore { get; set; } = 0;

  [ForeignKey("UserId")]
  public virtual User User { get; set; }
}

