using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WordRush.Core.Features;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository.Models;
using WordRush.Web.Models;

namespace WordRush.Web.Controllers
{
  [Route("auth")]
  public class AuthController(SignInManager<User> signInManager, IAuthService authService, UserManager<User> userManager, IRoleService roleService, IUserService userService) : ApiControllerBase
  {
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> SignIn([FromBody] LoginRequest request)
    {
      User? user = await GetUser(request.Email);

      if (user != null)
      {
        Microsoft.AspNetCore.Identity.SignInResult passValidationResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, true);

        if (passValidationResult.Succeeded)
        {
          LoginResponse response = await authService.Login(user);

          Log.Information(messageTemplate: "Authorization service Result => {@Response}", response);

          return Ok(response);
        }

        return BadRequest("Incorrect password");
      }

      return BadRequest("Incorrect email");
    }

    [HttpPost("sign-up")]
    [AllowAnonymous]
    public async Task<ActionResult<SignUpResponse>> SignUp([FromBody] SignUpRequest request)
    {
      bool userExists = await userService.GetUserByEmail(request.Email);

      if (userExists)
      {
        return BadRequest("Email already exists");
      }

      User user = await CreateUserFromRequest(request);
      IdentityResult response = await userManager.CreateAsync(user, request.Password);

      if (!response.Succeeded)
      {
        SignUpResponse signUpResponse = new()
        {
          Success = false,
          Message = string.Join("; ", response.Errors.Select(e => e.Description))
        };

        return BadRequest(signUpResponse);
      }

      SignUpResponse signup = new()
      {
        Success = true,
        Message = "User registered successfully",
        UserId = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        Nickname = user.Nickname,
        Avatar = user.Avatar,
        RoleId = user.RoleId
      };
      Log.Information(messageTemplate: "Signup Service Result => {@Signup}", signup);

      return signup;
    }


    [HttpPut("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
      User? user = await userService.GetUserProfileByEmail(request.Email);

      if (user == null)
      {
        return NotFound("User not found");
      }

      var token = await userManager.GeneratePasswordResetTokenAsync(user);

      var response = await userManager.ResetPasswordAsync(user, token, request.NewPassword);

      if (!response.Succeeded)
      {
        ResetPasswordResponse resetPasswordResponse = new()
        {
          Success = false,
          Message = string.Join("; ", response.Errors.Select(e => e.Description))
        };

        return BadRequest(resetPasswordResponse);
      }

      ResetPasswordResponse resetPassword = new()
      {
        Success = true,
        Message = "Password changed successfully",
      };
      Log.Information(messageTemplate: "ResetPassword Service Result => {@Signup}", resetPassword);

      return resetPassword;
    }

    private async Task<User> CreateUserFromRequest(SignUpRequest request)
    {
      Role role = await roleService.GetRoleById(1);
      User user = new()
      {
        UserName = request.UserName,
        Email = request.Email,
        Avatar = request.Avatar,
        Nickname = request.Nickname,
        Role = role
      };
      Log.Information(messageTemplate: "Create User Service Result => {@User}", user);
      return user;
    }

    private async Task<User?> GetUser(string identifier)
    {
      bool isEmail = new EmailAddressAttribute().IsValid(identifier);

      if (!isEmail)
      {
        return null;
      }

      User? user = await signInManager.UserManager.FindByEmailAsync(identifier);
      Log.Information(messageTemplate: "Get User Service Result => {@User}", user);
      return user;
    }
  }
}
