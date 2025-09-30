namespace WordRush.Web.Models;

public class SignUpResponse
{
  public bool Success { get; set; }

  public string Message { get; set; } = string.Empty;

  public int UserId { get; set; }

  public string UserName { get; set; } = string.Empty;

  public string Email { get; set; } = string.Empty;

  public string Nickname { get; set; } = string.Empty;

  public string Avatar { get; set; } = string.Empty;

  public int RoleId { get; set; }
}
