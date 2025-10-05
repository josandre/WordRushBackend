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
    public async Task<ActionResult<UserProfile?>> GetUserProfile(string userEmail)
    {
      bool isEmail = new EmailAddressAttribute().IsValid(userEmail);

      if (!isEmail)
      {
        return null;
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

      Log.Information(messageTemplate: "\nGet User Profile Service Result => {@User}", userProfile);
      return userProfile;
    }

    [HttpPut("update-profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfile))]
    public async Task<ActionResult<UserProfile?>> UpdateUserProfile([FromBody] UserProfile profile)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      bool isEmail = new EmailAddressAttribute().IsValid(profile.Email);

      if (!isEmail || profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Nickname) || string.IsNullOrWhiteSpace(profile.Avatar))
      {
        return BadRequest();
      }

      User? user = await profileService.UpdateUserProfile(profile.Id, profile.Nickname, profile.Avatar, profile.Email, profile.Password);

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
      return userProfile;
    }
  }
}
