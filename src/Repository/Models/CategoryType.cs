using System.ComponentModel.DataAnnotations;

namespace WordRush.Repository.Models;

public class CategoryType
{
  [Key]
  public int Id { get; set; }

  [Required]
  public string Name { get; set; }

  public ICollection<CategoryColumn> CategoryColumns { get; set; } = new List<CategoryColumn>();

}
