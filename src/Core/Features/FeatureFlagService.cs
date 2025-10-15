using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using Microsoft.Extensions.Configuration;

namespace WordRush.Core.Features;

public class FeatureFlagService : IFeatureFlagService, IDisposable
{
  private readonly LdClient client;

  public FeatureFlagService(IConfiguration config)
  {
    var sdkKey = config["LaunchDarkly:SdkKey"];
    var memberId = config["LaunchDarkly:MemberId"];

    var launchDarklyConfig = Configuration.Builder(sdkKey)
      .Build();

    client = new LdClient(launchDarklyConfig);

    if (client.Initialized)
    {
      var context = Context.New("server");
      client.Track(memberId, context);
    }
  }

  public IDictionary<string, bool> GetFlags(string userKey)
  {
    var context = Context.New("userKey");
    var flags = client.AllFlagsState(context);
    var result = new Dictionary<string, bool>();

    foreach (var flag in flags.ToValuesJsonMap())
    {
      if (flag.Value.Type == LdValueType.Bool)
      {
        result[flag.Key] = flag.Value.AsBool;
      }
    }

    return result;
  }

  public void Dispose()
  {
    client.Dispose();
  }
}
