namespace WordRush.Core.Features;

public interface IUserService
{
  Task<bool> GetUserByEmail(string email);
}
