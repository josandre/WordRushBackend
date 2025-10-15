using System.ComponentModel.DataAnnotations;

namespace WordRush.Web.Models;

public class ResetPasswordRequest
{
  [Required]
  [MaxLength(256)]
  public string Email { get; set; }

  [Required]
  [MaxLength(256)]
  public string NewPassword { get; set; }
}
