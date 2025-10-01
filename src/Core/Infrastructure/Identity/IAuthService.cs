using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository.Models;

namespace WordRush.Core.Infrastructure.Identity;

public interface IAuthService
{
  Task<LoginResponse> Login(User user);
}
