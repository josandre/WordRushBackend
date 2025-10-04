using System.ComponentModel.DataAnnotations;

namespace WordRush.Web.Features.UserProfile
{
  public class UserProfile
  {
    public UserProfile()
    {
    }

    public int Id { get; set; }

    public int RoleId { get; set; }

    public string? Nickname { get; set; }

    public string? Email { get; set; }

    [MaxLength(2048)]
    public string? Avatar { get; set; }
  }
}
