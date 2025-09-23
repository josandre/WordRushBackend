using System.ComponentModel.DataAnnotations;

namespace WordRush.Repository.Models;

public class Game
{
  [Key]
  public int Id { get; set; }

  public required string Name { get; set; }

  public required string Description { get; set; }
}
