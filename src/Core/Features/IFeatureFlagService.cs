namespace WordRush.Core.Features;

public interface IFeatureFlagService
{
  IDictionary<string, bool> GetFlags(string userKey);
}

