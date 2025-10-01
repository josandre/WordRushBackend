using System.ComponentModel.DataAnnotations;

namespace WordRush.Repository.Models;

public class Privilege
{
  [Key]
  public int Id { get; set; }

  [Required]
  [MaxLength(256)]
  public string Name { get; set; }
}
