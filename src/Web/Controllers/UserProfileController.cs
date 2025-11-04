using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WordRush.Core.Features;
using WordRush.Repository.Models;

namespace WordRush.Web.Controllers
{
  [Authorize]
  [Route("api/userProfile")]
  public class UserProfileController(IUserService _userService) : ApiControllerBase
  {
    private readonly IUserService userService = _userService;

    [HttpGet("get-profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(User))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<User?>> GetUserProfile(string userEmail)
    {
      bool isEmail = new EmailAddressAttribute().IsValid(userEmail);

      if (!isEmail)
      {
        return BadRequest("Bad Request");
      }

      User? user = await userService.GetUserProfileByEmail(userEmail);
      if (user != null)
      {
        Log.Information(messageTemplate: "\nGet User Profile Service Result => {@User}", user);
        return Ok(user);
      }
      else
      {
        return NotFound("User not Found!");
      }
    }

    [HttpPut("update-profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(User))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<User?>> UpdateUserProfile([FromBody] UpdateUserRequest profile)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      bool isEmail = new EmailAddressAttribute().IsValid(profile.Email);

      if (!isEmail || profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Nickname) || string.IsNullOrWhiteSpace(profile.Avatar))
      {
        return NotFound("User not Found!");
      }

      User? user = await userService.UpdateUserProfile(profile.Id, profile.Nickname, profile.Avatar, profile.Email);
      if (user != null)
      {
        Log.Information(messageTemplate: "\nUpdate User Service Result Failed => {@User}", user);
        return Ok(user);
      }
      else
      {
        Log.Information(messageTemplate: "\nUpdate User Service Result => {@User}", user);
        return BadRequest(user);
      }
    }
  }
}
