using System.ComponentModel.DataAnnotations;

namespace WordRush.Web.Models;

public class SignUpRequest
{
  [Required]
  public string UserName { get; set; } = string.Empty;

  [Required]
  public string Email { get; set; } = string.Empty;

  [Required]
  public string Password { get; set; } = string.Empty;

  [Required]
  public string Nickname { get; set; } = string.Empty;

  [Required]
  public string Avatar { get; set; } = string.Empty;

}
