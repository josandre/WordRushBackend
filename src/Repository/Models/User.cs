using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace WordRush.Repository.Models;

public class User : IdentityUser<int>
{
  public override int Id { get; set; }

  public int RoleId { get; set; }

  [Required]
  [MaxLength(256)]
  public string Nickname { get; set; }

  [Required]
  public override string Email { get; set; }

  public DateTimeOffset? LastActivityDate { get; set; }

  [Required]
  public DateTime CreatedOn { get; set; }

  public bool IsActive { get; set; } = true;

  [Required]
  public DateTime UpdatedOn { get; set; }

  [Required]
  [MaxLength(2048)]
  public string Avatar { get; set; }

  [ForeignKey("RoleId")]
  public virtual Role Role { get; set; }

  public virtual GameStatistics? GameStatistics { get; set; }
}
