using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Admin;

namespace WordRush.Web.Controllers
{
  /// <summary>
  /// Provides administrative endpoints to manage users.
  /// </summary>
  [ApiController]
  [Route("api/[controller]")]
  [Authorize] // Frontend additionally restricts access to RoleId == 2 (admin).
  public class AdminController : ControllerBase
  {
    private readonly IAdminService adminService;

    public AdminController(IAdminService adminService)
    {
      this.adminService = adminService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetUsers(
      [FromQuery] string search = null,
      [FromQuery] string sortBy = null,
      [FromQuery] bool ascending = true)
    {
      IReadOnlyList<AdminUserDto> result = await adminService.GetUsersAsync(search, sortBy, ascending);
      return Ok(result);
    }

    [HttpPost("user/{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
      bool updated = await adminService.ToggleUserActiveAsync(id);
      if (!updated)
      {
        return NotFound();
      }

      return NoContent();
    }

    [HttpPost("user/{id:int}/set-role")]
    public async Task<IActionResult> SetRole(int id, [FromQuery] int roleId)
    {
      bool ok = await adminService.SetUserRoleAsync(id, roleId);

      return !ok ? BadRequest("Unable to change role.") : NoContent();
    }
  }
}
