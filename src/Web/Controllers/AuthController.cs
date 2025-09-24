using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository.Models;
using WordRush.Web.Models;

namespace WordRush.Web.controllers;

[Microsoft.AspNetCore.Components.Route("auth")]
public class AuthController : ApiControllerBase
{
  private readonly SignInManager<User> signInManager;
  private readonly UserManager<User> userManager;
  private readonly IAuthService authService;


  public AuthController(SignInManager<User> signInManager, IAuthService authService, UserManager<User> userManager)
  {
    this.signInManager = signInManager;
    this.userManager = userManager;
    this.authService = authService;
  }

  [AllowAnonymous]
  [HttpPost("login")]
  public async Task<ActionResult<LoginResponse>> SignIn([FromBody] LoginRequest request)
  {
    var user = await this.GetUser(request.Email);

    if (user != null)
    {
      var passValidationResult = await this.signInManager.CheckPasswordSignInAsync(user, request.Password, true);

      if (passValidationResult.Succeeded)
      {
        var response = await this.authService.Login(user);
        return this.Ok(response);
      }

      return this.BadRequest("Incorrect password");
    }

    return this.BadRequest("Incorrect email");
  }

  private async Task<User> GetUser(string identifier)
  {
    var isEmail = new EmailAddressAttribute().IsValid(identifier);

    if (!isEmail)
    {
      return null;
    }

    return await this.signInManager.UserManager.FindByEmailAsync(identifier);
  }

}
