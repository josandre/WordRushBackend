using System.Security.Claims;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace WordRush.Web.Controllers;

public class ApiControllerBase: ControllerBase
{
  protected Uri GetCurrentRequestUriBase()
  {
    string clientUrl = Request.Headers[CorsConstants.Origin].ToString();
    return new Uri(clientUrl);
  }

  protected string? GetUserEmail()
  {
    return User.FindFirst(ClaimTypes.Email)?.Value;
  }
}
