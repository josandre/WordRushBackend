using System;

namespace WordRush.Core.Features.Admin
{
  /// <summary>
  /// A lightweight projection of user data and statistics for administrative views.
  /// </summary>
  public class AdminUserDto
  {
    public int Id { get; set; }

    public string Nickname { get; set; }

    public string Email { get; set; }

    public int RoleId { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTimeOffset? LastActivityDate { get; set; }

    public bool IsActive { get; set; }

    public int TotalPlayedGame { get; set; }

    public int WonGames { get; set; }

    public int TotalStore { get; set; }
  }
}
