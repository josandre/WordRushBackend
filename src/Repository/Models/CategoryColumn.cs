using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordRush.Repository.Models;

public class CategoryColumn
{
  [Key]
  public int Id { get; set; }

  [Required]
  public string Column { get; set; }

  [ForeignKey("CategoryTypeId")]
  public virtual CategoryType CategoryType { get; set; }
}
