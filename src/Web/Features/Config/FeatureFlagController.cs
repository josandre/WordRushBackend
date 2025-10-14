using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features;

namespace WordRush.Web.Features.Config;

[EnableCors]
[ApiController]
[Route("api/config")]
public class FeatureFlagController(IFeatureFlagService featureFlagService) : ControllerBase
{
  /// <summary>
  /// Returns a list with all feature flags in the system.
  /// </summary>
  /// <returns>200 OK with all feature flags.</returns>
  [HttpGet]
  [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IDictionary<string, bool>))]
  public ActionResult<IDictionary<string, bool>> Get()
  {
    var result = featureFlagService.GetFlags("server");
    return Ok(result);
  }
}
