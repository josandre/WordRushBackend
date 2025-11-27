using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WordRush.Core.Features.Game;
using WordRush.Web.Controllers;
using WordRush.Web.Features.Game.Models;

namespace WordRush.Web.Features.Game;

[Authorize]
[Route("api/game-room")]
[ApiController]
public class GameController : ApiControllerBase
{
  private readonly IGameSettingsService _gameSettingsService;

  public GameController(IGameSettingsService gameSettingsService)
  {
    _gameSettingsService = gameSettingsService;
  }

  [HttpPut("update-settings")]
  public async Task<ActionResult<UpdateGameSettingsResponse>> UpdateGameSettings([FromBody] UpdateGameSettingsRequest request)
  {
    if (!ModelState.IsValid)
    {
      return BadRequest(ModelState);
    }

    if (string.IsNullOrWhiteSpace(request.RoomId))
    {
      return BadRequest("RoomId is required");
    }

    if (request.Settings.Letters.Length > 5)
    {
      return BadRequest("Letters array cannot contain more than 5 elements");
    }

    GameRoom? updatedRoom = await _gameSettingsService.UpdateGameSettings(request.RoomId, request.Settings);

    if (updatedRoom == null)
    {
      Log.Warning("Game room not found: {RoomId}", request.RoomId);
      return NotFound($"Game room with ID '{request.RoomId}' not found");
    }

    Log.Information("Game settings updated for room: {RoomId}", request.RoomId);

    UpdateGameSettingsResponse response = new()
    {
      RoomId = updatedRoom.RoomId,
      Settings = updatedRoom.Settings
    };

    return Ok(response);
  }
}

