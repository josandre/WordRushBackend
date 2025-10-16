public class UpdateUserRequest
{
  public int Id { get; set; }

  public int RoleId { get; set; }

  public string? Nickname { get; set; } = string.Empty;

  public string? Email { get; set; } = string.Empty;

  public string? Avatar { get; set; } = string.Empty;
}
