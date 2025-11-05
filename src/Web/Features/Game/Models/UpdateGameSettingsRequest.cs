using System.ComponentModel.DataAnnotations;

namespace WordRush.Web.Features.Game.Models;

public class UpdateGameSettingsRequest
{
  [Required]
  public string RoomId { get; set; } = string.Empty;

  [Required]
  public GameSettings Settings { get; set; } = new();
}

