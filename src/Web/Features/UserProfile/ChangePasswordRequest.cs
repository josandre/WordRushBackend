public class ChangePasswordRequest
{
  public int Id { get; set; }

  public string CurrentPassword { get; set; } = string.Empty;

  public string NewPassword { get; set; } = string.Empty;

  public string ConfirmPassword { get; set; } = string.Empty;
}
