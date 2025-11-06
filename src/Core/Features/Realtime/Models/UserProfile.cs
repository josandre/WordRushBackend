namespace WordRush.Core.Features.Realtime.Models
{
  [Serializable]
  public class UserProfile
  {
    public string Nickname { get; set; } = "Player";

    public string Avatar { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
  }
}
