using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository.Models;
using WordRush.Web.Models;

namespace WordRush.Web.controllers;

[Route("auth")]
public class AuthController : ApiControllerBase
{
  private readonly SignInManager<User> signInManager;
  private readonly UserManager<User> userManager;
  private readonly IAuthService authService;
  private readonly IRoleService roleService;


  public AuthController(SignInManager<User> signInManager, IAuthService authService, UserManager<User> userManager, IRoleService roleService)
  {
    this.signInManager = signInManager;
    this.userManager = userManager;
    this.roleService = roleService;
    this.authService = authService;
  }

  [HttpPost("login")]
  [AllowAnonymous]
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

  [HttpPost("sign-up")]
  [AllowAnonymous]
  public async Task<ActionResult<SignUpResponse>> SignUp([FromBody] SignUpRequest request)
  {
    var user = await CreateUserFromRequest(request);
    var response = await this.userManager.CreateAsync(user, request.Password);

    if (!response.Succeeded)
    {
      var signUpResponse = new SignUpResponse()
      {
        Success = false,
        Message = string.Join("; ", response.Errors.Select(e => e.Description))
      };

      return this.BadRequest(signUpResponse);
    }

    return new SignUpResponse
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
  }

  private async Task<User> CreateUserFromRequest(SignUpRequest request)
  {

    Role role = await roleService.GetRoleById(1);

    var user = new User
    {
      UserName = request.UserName,
      Email = request.Email,
      Avatar = request.Avatar,
      Nickname = request.Nickname,
      Role = role
    };


    return user;
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
