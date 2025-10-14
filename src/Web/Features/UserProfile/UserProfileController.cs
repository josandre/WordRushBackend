using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WordRush.Core.Features;
using WordRush.Repository.Models;
using WordRush.Web.Controllers;

namespace WordRush.Web.Features.UserProfile
{
  [Authorize]
  [Route("api/userProfile")]
  public class UserProfileController(IProfileService profileService) : ApiControllerBase
  {
    private readonly IProfileService profileService = profileService;

    [HttpGet("get-profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfile))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile?>> GetUserProfile(string userEmail)
    {
      bool isEmail = new EmailAddressAttribute().IsValid(userEmail);

      if (!isEmail)
      {
        return BadRequest();
      }

      User? user = await profileService.GetUserProfileByEmail(userEmail);
      UserProfile userProfile = new();
      if (user != null)
      {
        userProfile.Id = user.Id;
        userProfile.RoleId = user.RoleId;
        userProfile.Email = user.Email;
        userProfile.Nickname = user.Nickname;
        userProfile.Avatar = user.Avatar;
      }
      else
      {
        return NotFound("Usuario no encontrado");
      }

      Log.Information(messageTemplate: "\nGet User Profile Service Result => {@User}", userProfile);
      return Ok(userProfile);
    }

    [HttpPut("update-profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfile))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile?>> UpdateUserProfile([FromBody] UserProfile profile)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      bool isEmail = new EmailAddressAttribute().IsValid(profile.Email);

      if (!isEmail || profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Nickname) || string.IsNullOrWhiteSpace(profile.Avatar))
      {
        return NotFound("Usuario no encontrado");
      }

      User? user = await profileService.UpdateUserProfile(profile.Id, profile.Nickname, profile.Avatar, profile.Email);

      UserProfile userProfile = new();
      if (user != null)
      {
        userProfile.Id = user.Id;
        userProfile.RoleId = user.RoleId;
        userProfile.Email = user.Email;
        userProfile.Nickname = user.Nickname;
        userProfile.Avatar = user.Avatar;
      }

      Log.Information(messageTemplate: "\nUpdate User Service Result => {@User}", userProfile);
      return Ok(userProfile);
    }

    [HttpPut("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      if (request.Id <= 0 ||
          string.IsNullOrWhiteSpace(request.CurrentPassword) ||
          string.IsNullOrWhiteSpace(request.NewPassword) ||
          string.IsNullOrWhiteSpace(request.ConfirmPassword))
      {
        return BadRequest("Todos los campos son requeridos.");
      }

      if (request.NewPassword != request.ConfirmPassword)
      {
        return BadRequest("Las contraseñas nuevas no coinciden.");
      }

      bool result = await profileService.ChangeUserPassword(
          request.Id,
          request.CurrentPassword,
          request.NewPassword);

      if (!result)
      {
        return NotFound("Usuario no encontrado o contraseña actual incorrecta.");
      }

      Log.Information("Password changed successfully for user {UserId}", request.Id);
      return Ok(true);
    }
  }
}
