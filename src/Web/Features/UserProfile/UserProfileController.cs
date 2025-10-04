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

    [HttpGet("getProfile")]
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

      Log.Information(messageTemplate: "Get User Service Result => {@User}", userProfile);
      return userProfile;
    }
  }
}
