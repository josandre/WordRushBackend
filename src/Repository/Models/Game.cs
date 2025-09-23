using System.ComponentModel.DataAnnotations;

namespace WordRush.Repository.Models;

public class Game
{
  [Key]
  public int Id { get; set; }

  public string Name { get; set; }

  public string Description { get; set; }
}
